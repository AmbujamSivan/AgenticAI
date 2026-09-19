using CommunicationProtocols.Application.Streaming;
using Microsoft.AspNetCore.SignalR;

namespace CommunicationProtocols.Host.SignalR;

/// <summary>
/// Bridges the transport-agnostic <see cref="StockNotifier"/> to SignalR: it
/// subscribes once and forwards every <see cref="StockChange"/> to all connected
/// hub clients via <c>IHubContext</c>. This is the idiomatic way to push into a hub
/// from outside a connection — and it reads from the *same* notifier the WebSocket
/// endpoint uses, so both transports carry an identical feed.
/// </summary>
public sealed class StockHubBroadcaster(StockNotifier notifier, IHubContext<StockHub> hub) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var subscription = notifier.Subscribe();
        try
        {
            await foreach (var change in subscription.Reader.ReadAllAsync(stoppingToken))
                await hub.Clients.All.SendAsync(StockHub.StockChangedEvent, change, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal on shutdown.
        }
    }
}
