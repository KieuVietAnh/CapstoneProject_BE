namespace UrbanService.BLL.Interfaces;

public sealed record AiFeedbackReviewQueueItem(Guid FeedbackId, Guid RequestedByUserId);

public interface IAiFeedbackReviewQueue
{
    ValueTask EnqueueAsync(
        Guid feedbackId,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AiFeedbackReviewQueueItem> DequeueAllAsync(CancellationToken cancellationToken);

    void MarkCompleted(Guid feedbackId);
}
