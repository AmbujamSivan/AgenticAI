using CommunicationProtocols.Application;
using CommunicationProtocols.Domain;

namespace CommunicationProtocols.Rag;

/// <summary>A search result: the matched product plus its similarity score.</summary>
public sealed record ProductSearchHit(Product Product, float Score);

/// <summary>
/// Ties the embedder, the vector store and the product catalog together:
/// <see cref="IndexAllAsync"/> embeds every product into the store, and
/// <see cref="SearchAsync"/> embeds a query, finds the nearest vectors, and resolves
/// them back to live <see cref="Product"/>s via <see cref="IProductService"/>.
/// </summary>
public sealed class ProductSearchService(
    ITextEmbedder embedder,
    IProductVectorStore store,
    IProductService products)
{
    public async Task IndexAllAsync(CancellationToken ct = default)
    {
        await store.EnsureCollectionAsync(embedder.Dimensions, ct);
        foreach (var product in await products.GetAllAsync(ct))
            await store.UpsertAsync(product.Id, embedder.Embed(ToText(product)), ct);
    }

    public async Task<IReadOnlyList<ProductSearchHit>> SearchAsync(string query, int topK, CancellationToken ct = default)
    {
        var queryVector = embedder.Embed(query);
        var matches = await store.SearchAsync(queryVector, topK, ct);

        var hits = new List<ProductSearchHit>(matches.Count);
        foreach (var match in matches)
        {
            var product = await products.GetByIdAsync(match.ProductId, ct);
            if (product is not null)
                hits.Add(new ProductSearchHit(product, match.Score));
        }
        return hits;
    }

    /// <summary>The text we embed per product — name plus description.</summary>
    private static string ToText(Product p) => $"{p.Name}. {p.Description}";
}
