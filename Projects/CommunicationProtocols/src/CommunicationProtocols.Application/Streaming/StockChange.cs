namespace CommunicationProtocols.Application.Streaming;

/// <summary>
/// A single stock movement, emitted by <see cref="StockTicker"/> and delivered to
/// clients over any streaming transport (WebSocket in Phase 5, SignalR in Phase 6).
/// Transport-agnostic on purpose — both transports serialize this same shape.
/// </summary>
public sealed record StockChange(
    Guid ProductId,
    string Name,
    int Stock,
    int Delta,
    DateTimeOffset At);
