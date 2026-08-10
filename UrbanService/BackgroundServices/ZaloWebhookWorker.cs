using UrbanService.BLL.Interfaces;

namespace UrbanService.BackgroundServices;

public class ZaloWebhookWorker : BackgroundService
{
    private const int MaximumAttempts = 3;

    private readonly IZaloWebhookQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ZaloWebhookWorker> _logger;

    public ZaloWebhookWorker(
        IZaloWebhookQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ZaloWebhookWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnqueueRecoverableEventsAsync(stoppingToken);

        await foreach (var webhookEventId in _queue.ReadAllAsync(stoppingToken))
        {
            var attemptCount = 0;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var inbox = scope.ServiceProvider.GetRequiredService<IZaloWebhookInbox>();
                var webhookEvent = await inbox.TryBeginProcessingAsync(webhookEventId, stoppingToken);
                if (webhookEvent == null)
                {
                    continue;
                }

                attemptCount = webhookEvent.AttemptCount;
                var zaloService = scope.ServiceProvider.GetRequiredService<IZaloService>();
                await zaloService.ProcessWebhookAsync(webhookEvent.Payload, stoppingToken);
                await inbox.MarkCompletedAsync(webhookEventId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to process Zalo webhook event {WebhookEventId} on attempt {AttemptCount}.",
                    webhookEventId,
                    attemptCount);

                using var failureScope = _scopeFactory.CreateScope();
                var inbox = failureScope.ServiceProvider.GetRequiredService<IZaloWebhookInbox>();
                await inbox.MarkFailedAsync(webhookEventId, exception.Message, stoppingToken);

                if (attemptCount < MaximumAttempts)
                {
                    var retryDelay = TimeSpan.FromSeconds(Math.Pow(2, Math.Max(1, attemptCount)));
                    await Task.Delay(retryDelay, stoppingToken);
                    await _queue.EnqueueAsync(webhookEventId, stoppingToken);
                }
            }
        }
    }

    private async Task EnqueueRecoverableEventsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var inbox = scope.ServiceProvider.GetRequiredService<IZaloWebhookInbox>();
        var eventIds = await inbox.GetRecoverableEventIdsAsync(cancellationToken);
        foreach (var eventId in eventIds)
        {
            await _queue.EnqueueAsync(eventId, cancellationToken);
        }

        if (eventIds.Count > 0)
        {
            _logger.LogInformation(
                "Queued {EventCount} recoverable Zalo webhook events.",
                eventIds.Count);
        }
    }
}
