using CommunicationProtocols.Application;
using CommunicationProtocols.Domain;

namespace CommunicationProtocols.Host.GraphQl;

/// <summary>
/// The GraphQL read surface. Each resolver is a thin adapter over
/// <see cref="IProductService"/> — the same service REST calls into. The service
/// is injected as a resolver parameter (HotChocolate resolves registered services).
/// </summary>
public sealed class Query
{
    /// <summary>All products.</summary>
    public Task<IReadOnlyList<Product>> GetProducts(IProductService products, CancellationToken ct)
        => products.GetAllAsync(ct);

    /// <summary>A single product by id, or null if not found.</summary>
    public Task<Product?> GetProduct(Guid id, IProductService products, CancellationToken ct)
        => products.GetByIdAsync(id, ct);
}
