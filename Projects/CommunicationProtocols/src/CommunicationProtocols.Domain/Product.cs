namespace CommunicationProtocols.Domain;

/// <summary>
/// The core domain entity. Every transport (REST, GraphQL, gRPC, WebSocket,
/// SignalR, and the Tier-2 agentic/MCP layer) ultimately reads and writes this
/// same type through the Application layer — it is the single source of truth.
/// </summary>
public sealed class Product
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public required string Description { get; set; }

    /// <summary>Unit price in the smallest currency unit's decimal form.</summary>
    public required decimal Price { get; set; }

    /// <summary>Units currently in stock. Streamed live over WebSocket/SignalR later.</summary>
    public required int Stock { get; set; }
}
