namespace UrbanService.BLL.Interfaces;

public interface IMessengerWebhookQueue
{
    ValueTask EnqueueAsync(string payload, CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken = default);
}
