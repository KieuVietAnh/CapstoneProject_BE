using UrbanService.BLL.Interfaces;

namespace UrbanService.BackgroundServices;

public class MessengerWebhookWorker : BackgroundService
{
    private readonly IMessengerWebhookQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MessengerWebhookWorker> _logger;

    public MessengerWebhookWorker(
        IMessengerWebhookQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<MessengerWebhookWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var payload in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var messengerService = scope.ServiceProvider.GetRequiredService<IMessengerService>();
                await messengerService.ProcessWebhookAsync(payload, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to process a Messenger webhook event.");
            }
        }
    }
}
