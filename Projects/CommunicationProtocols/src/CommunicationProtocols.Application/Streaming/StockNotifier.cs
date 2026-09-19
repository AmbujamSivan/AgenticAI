using System.Collections.Concurrent;
using System.Threading.Channels;

namespace CommunicationProtocols.Application.Streaming;

/// <summary>
/// A tiny transport-agnostic fan-out hub. The <see cref="StockTicker"/> publishes
/// <see cref="StockChange"/> events; each connected client (a WebSocket, a SignalR
/// connection, …) subscribes to get its own buffered stream. Using a channel per
/// subscriber keeps a slow client from blocking the others.
/// </summary>
public sealed class StockNotifier
{
    private readonly ConcurrentDictionary<Guid, Channel<StockChange>> _subscribers = new();

    /// <summary>Registers a subscriber. Dispose the returned handle to unsubscribe.</summary>
    public Subscription Subscribe()
    {
        var channel = Channel.CreateUnbounded<StockChange>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
        var id = Guid.NewGuid();
        _subscribers[id] = channel;
        return new Subscription(this, id, channel.Reader);
    }

    /// <summary>Broadcasts a change to every current subscriber (non-blocking).</summary>
    public void Publish(StockChange change)
    {
        foreach (var channel in _subscribers.Values)
            channel.Writer.TryWrite(change);
    }

    private void Remove(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel))
            channel.Writer.TryComplete();
    }

    /// <summary>A subscriber's read handle; disposing it unsubscribes.</summary>
    public sealed class Subscription(StockNotifier owner, Guid id, ChannelReader<StockChange> reader) : IDisposable
    {
        public ChannelReader<StockChange> Reader { get; } = reader;

        public void Dispose() => owner.Remove(id);
    }
}
