using System.Net.Http.Json;
using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.Services;

public class ApiCatalogService : ICatalogService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppSettings _settings;

    public ApiCatalogService(IHttpClientFactory httpClientFactory, AppSettings settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
    }

    public async Task<IEnumerable<ProductModel>> SearchAsync(string? query, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient();
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);

        var url = string.IsNullOrWhiteSpace(query)
            ? "/api/products?page=1&size=50"
            : $"/api/products?query={Uri.EscapeDataString(query)}&page=1&size=50";

        var result = await client.GetFromJsonAsync<PagedResultDto<ProductDto>>(url, ct);
        var items = result?.Items?.Select(p => new ProductModel
        {
            Id = p.Id,
            Name = p.Name,
            Sku = p.Sku,
            Unit = string.Empty,
            Price = p.UnitPrice
        }) ?? Enumerable.Empty<ProductModel>();
        return items;
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
    }
}
