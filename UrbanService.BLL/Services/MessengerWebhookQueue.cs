using System.Threading.Channels;
using UrbanService.BLL.Interfaces;

namespace UrbanService.BLL.Services;

public class MessengerWebhookQueue : IMessengerWebhookQueue
{
    private readonly Channel<string> _channel = Channel.CreateBounded<string>(
        new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(string payload, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(payload, cancellationToken);
    }

    public IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
