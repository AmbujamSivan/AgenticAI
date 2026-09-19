using Microsoft.AspNetCore.SignalR;

namespace CommunicationProtocols.Host.SignalR;

/// <summary>
/// The SignalR transport surface. Compared to the raw WebSocket in Phase 5, the hub
/// gives us connection lifetime, groups, reconnection and automatic message framing
/// for free — so this class is nearly empty. Server-push happens from
/// <see cref="StockHubBroadcaster"/> via <c>IHubContext</c>; clients just listen for
/// the "stockChanged" method. A hub method is included to show client → server calls.
/// </summary>
public sealed class StockHub : Hub
{
    /// <summary>The event name clients subscribe to (kept in one place).</summary>
    public const string StockChangedEvent = "stockChanged";

    /// <summary>Example client-callable method: a simple round-trip over the hub.</summary>
    public string Ping() => "pong";
}
