using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.Services;

public class MockCatalogService : ICatalogService
{
    private readonly List<ProductModel> _all = new()
    {
        new ProductModel { Id = 1, Sku = "OP-1", Name = "РћРџ-1 (РїРѕСЂРѕС€РєРѕРІС‹Р№) 1 РєРі", Unit = "С€С‚", Price = 150000m, Category = "РћРіРЅРµС‚СѓС€РёС‚РµР»Рё" },
        new ProductModel { Id = 2, Sku = "BR-OP5", Name = "РљСЂРѕРЅС€С‚РµР№РЅ РЅР°СЃС‚РµРЅРЅС‹Р№ РґР»СЏ РћРџ-5", Unit = "С€С‚", Price = 60000m, Category = "РљСЂРѕРЅС€С‚РµР№РЅС‹" },
        new ProductModel { Id = 3, Sku = "CAB-1", Name = "РЁРєР°С„ РґР»СЏ РѕРіРЅРµС‚СѓС€РёС‚РµР»СЏ (РјРµС‚Р°Р»Р»)", Unit = "С€С‚", Price = 450000m, Category = "РЁРєР°С„С‹" },
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
            cats = new[] { "РћРіРЅРµС‚СѓС€РёС‚РµР»Рё", "РљСЂРѕРЅС€С‚РµР№РЅС‹", "РџРѕРґСЃС‚Р°РІРєРё", "РЁРєР°С„С‹", "РґР°С‚С‡РёРєРё", "СЂСѓРєР°РІР°" }
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList();
        }
        return Task.FromResult<IEnumerable<string>>(cats);
    }
}

