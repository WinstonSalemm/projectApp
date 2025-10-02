using System.Collections.Concurrent;
using System.Linq;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Repositories;

public class InMemoryProductRepository : IProductRepository
{
    private readonly List<Product> _products;

    public InMemoryProductRepository()
    {
        _products = Seed();
    }

    public Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return Task.FromResult(_products.FirstOrDefault(p => p.Id == id));
    }

    public Task<int> CountAsync(string? query, string? category = null, CancellationToken ct = default)
    {
        var filtered = ApplyQuery(_products.AsEnumerable(), query, category);
        return Task.FromResult(filtered.Count());
    }

    public Task<IEnumerable<Product>> SearchAsync(string? query, int page, int size, string? category = null, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 50;

        var filtered = ApplyQuery(_products.AsEnumerable(), query, category)
            .OrderBy(p => p.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToArray();

        return Task.FromResult<IEnumerable<Product>>(filtered);
    }

    public Task<IEnumerable<string>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var cats = _products.Select(p => p.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c)
            .AsEnumerable();
        return Task.FromResult(cats);
    }

    public Task<Product> AddAsync(Product p, CancellationToken ct = default)
    {
        var nextId = _products.Count == 0 ? 1 : _products.Max(x => x.Id) + 1;
        p.Id = nextId;
        _products.Add(p);
        return Task.FromResult(p);
    }

    private static IEnumerable<Product> ApplyQuery(IEnumerable<Product> products, string? query, string? category)
    {
        var res = products;
        if (!string.IsNullOrWhiteSpace(category))
        {
            var c = category.Trim();
            res = res.Where(p => string.Equals(p.Category, c, StringComparison.Ordinal));
        }
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            res = res.Where(p =>
                p.Sku.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
        return res;
    }

    private static List<Product> Seed()
    {
        return new List<Product>
        {
            new() { Id = 1, Sku = "SKU-001", Name = "Coffee Beans 1kg", Unit = "kg", Price = 15.99m, Category = "Demo" },
            new() { Id = 2, Sku = "SKU-002", Name = "Tea Leaves 500g", Unit = "pkg", Price = 8.49m, Category = "Demo" },
            new() { Id = 3, Sku = "SKU-003", Name = "Sugar 1kg", Unit = "kg", Price = 2.29m, Category = "Demo" },
            new() { Id = 4, Sku = "SKU-004", Name = "Milk 1L", Unit = "ltr", Price = 1.19m, Category = "Demo" },
            new() { Id = 5, Sku = "SKU-005", Name = "Butter 200g", Unit = "pkg", Price = 3.79m, Category = "Demo" },
            new() { Id = 6, Sku = "SKU-006", Name = "Bread Loaf", Unit = "pc", Price = 1.99m, Category = "Demo" },
            new() { Id = 7, Sku = "SKU-007", Name = "Eggs (12)", Unit = "box", Price = 2.99m, Category = "Demo" },
            new() { Id = 8, Sku = "SKU-008", Name = "Olive Oil 500ml", Unit = "btl", Price = 6.49m, Category = "Demo" },
            new() { Id = 9, Sku = "SKU-009", Name = "Pasta 1kg", Unit = "kg", Price = 2.59m, Category = "Demo" },
            new() { Id = 10, Sku = "SKU-010", Name = "Tomato Sauce 300g", Unit = "jar", Price = 2.39m, Category = "Demo" },
        };
    }
}
