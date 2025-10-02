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

    public async Task<IEnumerable<ProductModel>> SearchAsync(string? query, string? category = null, CancellationToken ct = default)
    {
        await Task.Delay(150, ct); // simulate latency
        var list = _all.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(category))
        {
            var c = category.Trim().ToLowerInvariant();
            list = list.Where(p => (p.Category ?? string.Empty).ToLowerInvariant() == c);
        }
        if (string.IsNullOrWhiteSpace(query)) return list;
        var q = query.Trim().ToLowerInvariant();
        return list.Where(p => p.Sku.ToLowerInvariant().Contains(q) || p.Name.ToLowerInvariant().Contains(q));
    }

    public Task<IEnumerable<string>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var cats = _all.Select(p => p.Category).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c).AsEnumerable();
        return Task.FromResult(cats);
    }
}
