using CommunicationProtocols.Application;
using CommunicationProtocols.Domain;

namespace CommunicationProtocols.Host.GraphQl;

/// <summary>GraphQL input for creating a product.</summary>
public sealed record CreateProductInput(string Name, string Description, decimal Price, int Stock);

/// <summary>GraphQL input for adjusting stock.</summary>
public sealed record AdjustStockInput(Guid Id, int Delta);

/// <summary>
/// The GraphQL write surface. Like the query resolvers, each mutation is a thin
/// adapter over <see cref="IProductService"/> and holds no business logic.
/// </summary>
public sealed class Mutation
{
    public Task<Product> CreateProduct(CreateProductInput input, IProductService products, CancellationToken ct)
        => products.CreateAsync(input.Name, input.Description, input.Price, input.Stock, ct);

    /// <summary>Adjusts stock by a positive or negative delta. Returns null if the product does not exist.</summary>
    public Task<Product?> AdjustStock(AdjustStockInput input, IProductService products, CancellationToken ct)
        => products.AdjustStockAsync(input.Id, input.Delta, ct);
}
