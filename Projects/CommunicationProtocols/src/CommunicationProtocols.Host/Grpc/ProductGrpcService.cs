using CommunicationProtocols.Application;
using CommunicationProtocols.Domain;
using Grpc.Core;

namespace CommunicationProtocols.Host.Grpc;

/// <summary>
/// The gRPC transport adapter. It implements the protobuf-generated
/// <see cref="ProductCatalog.ProductCatalogBase"/> and maps the generated message
/// types to/from the shared <see cref="IProductService"/>. As with every other
/// transport, there is no business logic here — only mapping.
/// </summary>
public sealed class ProductGrpcService(IProductService products) : ProductCatalog.ProductCatalogBase
{
    public override async Task<GetProductsReply> GetProducts(GetProductsRequest request, ServerCallContext context)
    {
        var items = await products.GetAllAsync(context.CancellationToken);
        var reply = new GetProductsReply();
        reply.Products.AddRange(items.Select(ToMessage));
        return reply;
    }

    public override async Task<ProductMessage> GetProduct(GetProductRequest request, ServerCallContext context)
    {
        var product = await products.GetByIdAsync(ParseId(request.Id), context.CancellationToken);
        return product is null ? throw NotFound(request.Id) : ToMessage(product);
    }

    public override async Task<ProductMessage> CreateProduct(CreateProductRequest request, ServerCallContext context)
    {
        var product = await products.CreateAsync(
            request.Name, request.Description, (decimal)request.Price, request.Stock, context.CancellationToken);
        return ToMessage(product);
    }

    public override async Task<ProductMessage> AdjustStock(AdjustStockRequest request, ServerCallContext context)
    {
        var product = await products.AdjustStockAsync(ParseId(request.Id), request.Delta, context.CancellationToken);
        return product is null ? throw NotFound(request.Id) : ToMessage(product);
    }

    private static ProductMessage ToMessage(Product p) => new()
    {
        Id = p.Id.ToString(),
        Name = p.Name,
        Description = p.Description,
        Price = (double)p.Price,
        Stock = p.Stock,
    };

    private static Guid ParseId(string id) =>
        Guid.TryParse(id, out var guid)
            ? guid
            : throw new RpcException(new Status(StatusCode.InvalidArgument, $"'{id}' is not a valid product id."));

    private static RpcException NotFound(string id) =>
        new(new Status(StatusCode.NotFound, $"Product '{id}' was not found."));
}
