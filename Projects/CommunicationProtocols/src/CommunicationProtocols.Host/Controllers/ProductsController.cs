using CommunicationProtocols.Application;
using CommunicationProtocols.Host.Rest;
using Microsoft.AspNetCore.Mvc;

namespace CommunicationProtocols.Host.Controllers;

/// <summary>
/// The REST transport adapter, as a classic ASP.NET Core Web API controller.
/// It translates HTTP into calls on <see cref="IProductService"/> and holds no
/// business logic — the shared Application layer remains the single source of truth.
/// </summary>
[ApiController]
[Route("api/products")]
[Produces("application/json")]
public sealed class ProductsController(IProductService products) : ControllerBase
{
    /// <summary>List all products.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<ProductResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductResponse>>> GetAll(CancellationToken ct)
    {
        var items = await products.GetAllAsync(ct);
        return Ok(items.Select(ProductResponse.From));
    }

    /// <summary>Get a single product by id.</summary>
    [HttpGet("{id:guid}", Name = "GetProductById")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(id, ct);
        return product is null ? NotFound() : Ok(ProductResponse.From(product));
    }

    /// <summary>Create a new product.</summary>
    [HttpPost]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest req, CancellationToken ct)
    {
        var product = await products.CreateAsync(req.Name, req.Description, req.Price, req.Stock, ct);
        return CreatedAtRoute("GetProductById", new { id = product.Id }, ProductResponse.From(product));
    }

    /// <summary>Adjust a product's stock by a positive or negative delta.</summary>
    [HttpPost("{id:guid}/adjust-stock")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> AdjustStock(Guid id, AdjustStockRequest req, CancellationToken ct)
    {
        var product = await products.AdjustStockAsync(id, req.Delta, ct);
        return product is null ? NotFound() : Ok(ProductResponse.From(product));
    }
}
