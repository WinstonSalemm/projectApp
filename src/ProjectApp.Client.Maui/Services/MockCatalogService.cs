using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.Services;

public class MockCatalogService : ICatalogService
{
    private readonly List<ProductModel> _all = new()
    {
        new ProductModel { Id = 1, Sku = "OP-1", Name = "ОП-1 (порошковый) 1 кг", Unit = "шт", Price = 150000m, Category = "Огнетушители" },
        new ProductModel { Id = 2, Sku = "BR-OP5", Name = "Кронштейн настенный для ОП-5", Unit = "шт", Price = 60000m, Category = "Кронштейны" },
        new ProductModel { Id = 3, Sku = "CAB-1", Name = "Шкаф для огнетушителя (металл)", Unit = "шт", Price = 450000m, Category = "Шкафы" },
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
        var cats = _all.Select(p => p.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();
        if (cats.Count == 0)
        {
            cats = new[] { "Огнетушители", "Кронштейны", "Подставки", "Шкафы", "датчики", "рукава" }
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList();
        }
        return Task.FromResult<IEnumerable<string>>(cats);
    }
}

