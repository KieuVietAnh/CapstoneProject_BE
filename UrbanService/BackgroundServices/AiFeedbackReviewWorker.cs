using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BackgroundServices;

public class AiFeedbackReviewWorker : BackgroundService
{
    private static readonly TimeSpan SubmittedScanInterval = TimeSpan.FromSeconds(60);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAiFeedbackReviewQueue _queue;
    private readonly ILogger<AiFeedbackReviewWorker> _logger;

    public AiFeedbackReviewWorker(
        IServiceScopeFactory scopeFactory,
        IAiFeedbackReviewQueue queue,
        ILogger<AiFeedbackReviewWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scanner = ScanSubmittedFeedbacksAsync(stoppingToken);
        var processor = ProcessQueueAsync(stoppingToken);

        await Task.WhenAll(scanner, processor);
    }

    private async Task ScanSubmittedFeedbacksAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SubmittedScanInterval);

        do
        {
            try
            {
                await EnqueueSubmittedFeedbacksAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to scan Submitted feedbacks for AI review queue.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task EnqueueSubmittedFeedbacksAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var pendingFeedbacks = await uow.GetRepository<Feedback>().Entities
            .AsNoTracking()
            .Where(f => f.Status == FeedbackStatus.Submitted)
            .OrderBy(f => f.CreatedAt)
            .Select(f => new
            {
                f.FeedbackId,
                f.UserId
            })
            .Take(1000)
            .ToListAsync(stoppingToken);

        foreach (var feedback in pendingFeedbacks)
        {
            await _queue.EnqueueAsync(feedback.FeedbackId, feedback.UserId, stoppingToken);
        }
    }

    private async Task ProcessQueueAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var aiFeedbackAnalysisService =
                    scope.ServiceProvider.GetRequiredService<IAiFeedbackAnalysisService>();

                _logger.LogInformation(
                    "Starting AI review for feedback {FeedbackId}.",
                    item.FeedbackId);

                await aiFeedbackAnalysisService.AnalyzeFeedbackAsync(
                    item.FeedbackId,
                    item.RequestedByUserId,
                    stoppingToken);

                _logger.LogInformation(
                    "Finished AI review for feedback {FeedbackId}.",
                    item.FeedbackId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "AI review failed for feedback {FeedbackId}. It will remain Submitted and be retried by the next scan.",
                    item.FeedbackId);
            }
            finally
            {
                _queue.MarkCompleted(item.FeedbackId);
            }
        }
    }
}
