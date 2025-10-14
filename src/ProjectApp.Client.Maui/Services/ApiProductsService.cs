using System.Net.Http.Json;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.Services;

public class ApiProductsService : IProductsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppSettings _settings;
    private readonly AuthService _auth;

    public ApiProductsService(IHttpClientFactory httpClientFactory, AppSettings settings, AuthService auth)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _auth = auth;
    }

    public async Task<bool> CreateCategoryAsync(string name, CancellationToken ct = default)
    {
        var n = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(n)) return false;
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);
        var resp = await client.PostAsJsonAsync("/api/categories", new { name = n }, ct);
        return resp.IsSuccessStatusCode;
    }

    private class ProductCreateDto
    {
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Category { get; set; } = string.Empty;
    }
    private class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public string? Category { get; set; }
    }

    public async Task<int?> CreateProductAsync(ProductCreateDraft draft, CancellationToken ct = default)
    {
        if (draft == null) return null;
        var dto = new ProductCreateDto
        {
            Sku = draft.Sku?.Trim() ?? string.Empty,
            Name = draft.Name?.Trim() ?? string.Empty,
            Unit = draft.Unit?.Trim() ?? "С€С‚",
            Price = draft.Price,
            Category = draft.Category?.Trim() ?? string.Empty
        };
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);
        var resp = await client.PostAsJsonAsync("/api/products", dto, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var pd = await resp.Content.ReadFromJsonAsync<ProductDto>(cancellationToken: ct);
        return pd?.Id;
    }
}

