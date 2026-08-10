using System.Threading.Channels;
using UrbanService.BLL.Interfaces;

namespace UrbanService.BLL.Services;

public class ZaloWebhookQueue : IZaloWebhookQueue
{
    private readonly Channel<long> _channel = Channel.CreateUnbounded<long>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(long webhookEventId, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(webhookEventId, cancellationToken);
    }

    public IAsyncEnumerable<long> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
