using UrbanService.DAL.Entities;

namespace UrbanService.BLL.Interfaces;

public interface IZaloWebhookInbox
{
    Task<long?> StoreAsync(string payload, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<long>> GetRecoverableEventIdsAsync(
        CancellationToken cancellationToken = default);

    Task<ZaloWebhookEvent?> TryBeginProcessingAsync(
        long webhookEventId,
        CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(long webhookEventId, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        long webhookEventId,
        string error,
        CancellationToken cancellationToken = default);
}
