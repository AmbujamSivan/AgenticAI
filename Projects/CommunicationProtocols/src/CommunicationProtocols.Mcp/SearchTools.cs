using System.ComponentModel;
using CommunicationProtocols.Rag;
using ModelContextProtocol.Server;

namespace CommunicationProtocols.Mcp;

/// <summary>
/// RAG tool: semantic product search backed by the vector store. This is the payoff
/// of Tier 2 — an agent can ask by intent ("something for a home office") rather than
/// by exact keyword or id. <see cref="ProductSearchService"/> is injected from DI.
/// </summary>
[McpServerToolType]
public sealed class SearchTools
{
    [McpServerTool(Name = "semantic_search")]
    [Description("Search the product catalog by meaning/intent and return the closest matches with a similarity score. Use this when the user describes what they want rather than naming a product.")]
    public static async Task<IReadOnlyList<object>> SemanticSearch(
        [Description("A natural-language description of what the user is looking for.")] string query,
        ProductSearchService search,
        [Description("How many matches to return (default 3).")] int topK = 3,
        CancellationToken ct = default)
    {
        var hits = await search.SearchAsync(query, topK, ct);
        return hits
            .Select(h => (object)new
            {
                id = h.Product.Id,
                name = h.Product.Name,
                description = h.Product.Description,
                price = h.Product.Price,
                stock = h.Product.Stock,
                score = h.Score,
            })
            .ToList();
    }
}
