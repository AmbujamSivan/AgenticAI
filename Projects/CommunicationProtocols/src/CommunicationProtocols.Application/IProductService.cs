using CommunicationProtocols.Domain;

namespace CommunicationProtocols.Application;

/// <summary>
/// The transport-agnostic contract for working with products. Each protocol in
/// this project (REST, GraphQL, gRPC, WebSocket, SignalR, MCP) is a thin adapter
/// over this interface — no business logic lives in the transports themselves.
/// </summary>
public interface IProductService
{
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default);

    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Product> CreateAsync(string name, string description, decimal price, int stock, CancellationToken ct = default);

    /// <summary>Adjusts stock by <paramref name="delta"/> (can be negative). Returns the updated product, or null if not found.</summary>
    Task<Product?> AdjustStockAsync(Guid id, int delta, CancellationToken ct = default);
}
