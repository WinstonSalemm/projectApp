using System.Net.Http.Json;
using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.Services;

public class ApiCatalogService : ICatalogService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppSettings _settings;
    private readonly AuthService _auth;

    public ApiCatalogService(IHttpClientFactory httpClientFactory, AppSettings settings, AuthService auth)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _auth = auth;
    }

    public async Task<IEnumerable<ProductModel>> SearchAsync(string? query, string? category = null, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient();
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);

        var q = string.IsNullOrWhiteSpace(query) ? null : Uri.EscapeDataString(query);
        var cat = string.IsNullOrWhiteSpace(category) ? null : Uri.EscapeDataString(category);
        var parts = new List<string> { "page=1", "size=50" };
        if (!string.IsNullOrEmpty(q)) parts.Add($"query={q}");
        if (!string.IsNullOrEmpty(cat)) parts.Add($"category={cat}");
        var url = "/api/products?" + string.Join("&", parts);

        var result = await client.GetFromJsonAsync<PagedResultDto<ProductDto>>(url, ct);
        var items = result?.Items?.Select(p => new ProductModel
        {
            Id = p.Id,
            Name = p.Name,
            Sku = p.Sku,
            Unit = string.Empty,
            Price = p.UnitPrice,
            Category = p.Category ?? string.Empty
        }) ?? Enumerable.Empty<ProductModel>();
        return items;
    }

    public async Task<IEnumerable<string>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient();
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);
        var list = await client.GetFromJsonAsync<List<string>>("/api/products/categories", ct);
        return list ?? Enumerable.Empty<string>();
    }

    private class PagedResultDto<T>
    {
        public List<T> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int Size { get; set; }
    }

    private class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public string? Category { get; set; }
    }
}
