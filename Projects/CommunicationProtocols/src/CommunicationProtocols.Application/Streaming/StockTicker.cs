using Microsoft.Extensions.Hosting;

namespace CommunicationProtocols.Application.Streaming;

/// <summary>
/// A background service that simulates live stock movement: every couple of seconds
/// it nudges a random product's stock via <see cref="IProductService"/> and publishes
/// the change through <see cref="StockNotifier"/>. This gives the streaming transports
/// (WebSocket, SignalR) a real, continuously-changing feed to push to clients.
/// </summary>
public sealed class StockTicker(IProductService products, StockNotifier notifier) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rng = new Random();
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(Interval, stoppingToken);

                var all = await products.GetAllAsync(stoppingToken);
                if (all.Count == 0)
                    continue;

                var target = all[rng.Next(all.Count)];
                var delta = rng.Next(1, 4) * (rng.Next(2) == 0 ? -1 : 1); // ±1..3

                var updated = await products.AdjustStockAsync(target.Id, delta, stoppingToken);
                if (updated is null)
                    continue;

                notifier.Publish(new StockChange(
                    updated.Id, updated.Name, updated.Stock, delta, DateTimeOffset.UtcNow));
            }
        }
        catch (OperationCanceledException)
        {
            // Normal on shutdown.
        }
    }
}
