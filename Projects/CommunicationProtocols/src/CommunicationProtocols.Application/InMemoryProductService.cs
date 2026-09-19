using System.Collections.Concurrent;
using CommunicationProtocols.Domain;

namespace CommunicationProtocols.Application;

/// <summary>
/// A simple thread-safe in-memory implementation, seeded with a few products.
/// Phase 1 keeps persistence out of scope so we can focus on transports; a real
/// store (EF Core, etc.) can replace this later without touching any transport.
/// </summary>
public sealed class InMemoryProductService : IProductService
{
    private readonly ConcurrentDictionary<Guid, Product> _products = new();

    public InMemoryProductService()
    {
        foreach (var p in Seed())
            _products[p.Id] = p;
    }

    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Product>>(_products.Values.OrderBy(p => p.Name).ToList());

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_products.GetValueOrDefault(id));

    public Task<Product> CreateAsync(string name, string description, decimal price, int stock, CancellationToken ct = default)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Price = price,
            Stock = stock,
        };
        _products[product.Id] = product;
        return Task.FromResult(product);
    }

    public Task<Product?> AdjustStockAsync(Guid id, int delta, CancellationToken ct = default)
    {
        if (!_products.TryGetValue(id, out var product))
            return Task.FromResult<Product?>(null);

        // ConcurrentDictionary access is per-key; lock the instance for a safe read-modify-write.
        lock (product)
        {
            product.Stock = Math.Max(0, product.Stock + delta);
        }
        return Task.FromResult<Product?>(product);
    }

    private static IEnumerable<Product> Seed() =>
    [
        new Product { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Aeron Chair",       Description = "Ergonomic office chair with lumbar support.",        Price = 1195.00m, Stock = 12 },
        new Product { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Mechanical Keyboard", Description = "Hot-swappable 75% keyboard with tactile switches.",    Price = 149.99m,  Stock = 40 },
        new Product { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "4K Monitor",          Description = "27-inch 4K IPS display, USB-C 90W power delivery.",    Price = 449.50m,  Stock = 7  },
    ];
}
