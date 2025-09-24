using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.Services;

public class MockCatalogService : ICatalogService
{
    private readonly List<ProductModel> _all = new()
    {
        new ProductModel { Id = 1, Sku = "SKU-001", Name = "Coffee Beans 1kg", Unit = "kg", Price = 15.99m },
        new ProductModel { Id = 2, Sku = "SKU-002", Name = "Tea Leaves 500g", Unit = "pkg", Price = 8.49m },
        new ProductModel { Id = 3, Sku = "SKU-003", Name = "Sugar 1kg", Unit = "kg", Price = 2.29m },
    };

    public async Task<IEnumerable<ProductModel>> SearchAsync(string? query, CancellationToken ct = default)
    {
        await Task.Delay(150, ct); // simulate latency
        if (string.IsNullOrWhiteSpace(query)) return _all;
        var q = query.Trim().ToLowerInvariant();
        return _all.Where(p => p.Sku.ToLowerInvariant().Contains(q) || p.Name.ToLowerInvariant().Contains(q));
    }
}
