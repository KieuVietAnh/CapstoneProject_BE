using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<Guid, DateTime> _retryAfterUtcByFeedbackId = new();
    private readonly TimeSpan _failureRetryDelay;

    public AiFeedbackReviewWorker(
        IServiceScopeFactory scopeFactory,
        IAiFeedbackReviewQueue queue,
        ILogger<AiFeedbackReviewWorker> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
        _failureRetryDelay = TimeSpan.FromMinutes(
            int.TryParse(configuration["AI:ReviewFailureRetryDelayMinutes"], out var retryDelayMinutes)
                ? Math.Clamp(retryDelayMinutes, 1, 1440)
                : 15);
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

        var now = DateTime.UtcNow;
        var newlyQueuedCount = 0;
        var cooldownSkippedCount = 0;

        foreach (var feedback in pendingFeedbacks)
        {
            if (_retryAfterUtcByFeedbackId.TryGetValue(feedback.FeedbackId, out var retryAfterUtc))
            {
                if (retryAfterUtc > now)
                {
                    cooldownSkippedCount++;
                    continue;
                }

                _retryAfterUtcByFeedbackId.TryRemove(feedback.FeedbackId, out _);
            }

            if (await _queue.EnqueueAsync(feedback.FeedbackId, feedback.UserId, stoppingToken))
            {
                newlyQueuedCount++;
            }
        }

        if (pendingFeedbacks.Count > 0)
        {
            _logger.LogInformation(
                "AI review scan found {SubmittedCount} Submitted feedbacks; queued {NewlyQueuedCount} new items; skipped {CooldownSkippedCount} items in retry cooldown; in-memory queue count is {QueueCount}.",
                pendingFeedbacks.Count,
                newlyQueuedCount,
                cooldownSkippedCount,
                _queue.QueuedCount);
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
                _retryAfterUtcByFeedbackId.TryRemove(item.FeedbackId, out _);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _retryAfterUtcByFeedbackId[item.FeedbackId] = DateTime.UtcNow.Add(_failureRetryDelay);

                _logger.LogWarning(
                    ex,
                    "AI review failed for feedback {FeedbackId}. Error: {ErrorMessage}. It will remain Submitted and be retried after {RetryDelayMinutes} minutes.",
                    item.FeedbackId,
                    ex.Message,
                    _failureRetryDelay.TotalMinutes);
            }
            finally
            {
                _queue.MarkCompleted(item.FeedbackId);
            }
        }
    }
}
