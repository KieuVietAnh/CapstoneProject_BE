namespace UrbanService.BLL.Interfaces;

public interface IZaloWebhookQueue
{
    ValueTask EnqueueAsync(long webhookEventId, CancellationToken cancellationToken = default);

    IAsyncEnumerable<long> ReadAllAsync(CancellationToken cancellationToken = default);
}
