using System.ComponentModel;
using CommunicationProtocols.Application;
using CommunicationProtocols.Domain;
using ModelContextProtocol.Server;

namespace CommunicationProtocols.Mcp;

/// <summary>
/// The MCP transport adapter: the product catalog exposed as tools an AI agent can
/// call. Each tool is a thin wrapper over the shared <see cref="IProductService"/>
/// (injected from DI into the tool method) — the same service REST, GraphQL and gRPC
/// use. The [Description] attributes are what the model reads to decide when to call.
/// </summary>
[McpServerToolType]
public sealed class ProductTools
{
    [McpServerTool(Name = "list_products")]
    [Description("List every product in the catalog with its price and current stock.")]
    public static async Task<IReadOnlyList<Product>> ListProducts(
        IProductService products, CancellationToken ct)
        => await products.GetAllAsync(ct);

    [McpServerTool(Name = "get_product")]
    [Description("Get a single product by its id. Returns null if no product has that id.")]
    public static async Task<Product?> GetProduct(
        [Description("The product's GUID id.")] Guid id,
        IProductService products, CancellationToken ct)
        => await products.GetByIdAsync(id, ct);

    [McpServerTool(Name = "create_product")]
    [Description("Create a new product and return it (including its generated id).")]
    public static async Task<Product> CreateProduct(
        [Description("Product name.")] string name,
        [Description("Short description.")] string description,
        [Description("Unit price.")] decimal price,
        [Description("Initial stock quantity.")] int stock,
        IProductService products, CancellationToken ct)
        => await products.CreateAsync(name, description, price, stock, ct);

    [McpServerTool(Name = "adjust_stock")]
    [Description("Adjust a product's stock by a delta (negative to decrement). Returns the updated product, or null if the id is unknown.")]
    public static async Task<Product?> AdjustStock(
        [Description("The product's GUID id.")] Guid id,
        [Description("Amount to add to stock; use a negative number to remove stock.")] int delta,
        IProductService products, CancellationToken ct)
        => await products.AdjustStockAsync(id, delta, ct);
}
