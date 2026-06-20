using System.Collections.Concurrent;
using System.Threading.Channels;
using UrbanService.BLL.Interfaces;

namespace UrbanService.BLL.Services;

public class AiFeedbackReviewQueue : IAiFeedbackReviewQueue
{
    private readonly Channel<AiFeedbackReviewQueueItem> _queue;
    private readonly ConcurrentDictionary<Guid, byte> _queuedFeedbackIds = new();

    public AiFeedbackReviewQueue()
    {
        _queue = Channel.CreateUnbounded<AiFeedbackReviewQueueItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(
        Guid feedbackId,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (!_queuedFeedbackIds.TryAdd(feedbackId, 0))
        {
            return ValueTask.CompletedTask;
        }

        return _queue.Writer.WriteAsync(
            new AiFeedbackReviewQueueItem(feedbackId, requestedByUserId),
            cancellationToken);
    }

    public IAsyncEnumerable<AiFeedbackReviewQueueItem> DequeueAllAsync(CancellationToken cancellationToken)
    {
        return _queue.Reader.ReadAllAsync(cancellationToken);
    }

    public void MarkCompleted(Guid feedbackId)
    {
        _queuedFeedbackIds.TryRemove(feedbackId, out _);
    }
}
