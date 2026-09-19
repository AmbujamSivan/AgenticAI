using CommunicationProtocols.Domain;

namespace CommunicationProtocols.Host.Rest;

/// <summary>
/// Transport-specific shapes for the REST adapter. Keeping request/response DTOs
/// in the transport (rather than in Domain/Application) is deliberate: each protocol
/// owns how it represents the domain, while the domain type stays untouched.
/// </summary>
public sealed record ProductResponse(Guid Id, string Name, string Description, decimal Price, int Stock)
{
    public static ProductResponse From(Product p) => new(p.Id, p.Name, p.Description, p.Price, p.Stock);
}

public sealed record CreateProductRequest(string Name, string Description, decimal Price, int Stock);

public sealed record AdjustStockRequest(int Delta);
