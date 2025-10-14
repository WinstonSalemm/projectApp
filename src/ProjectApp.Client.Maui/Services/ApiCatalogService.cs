using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ProjectApp.Client.Maui.Models;
using ProjectApp.Client.Maui.Utils;

namespace ProjectApp.Client.Maui.Services;

public class ApiCatalogService : ICatalogService
{
    private static readonly JsonSerializerOptions CategoriesSerializerOptions = new(JsonSerializerDefaults.Web);

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
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);

        // Map UI placeholders to API filters
        var categoryRaw = category?.Trim();
        if (string.Equals(categoryRaw, "(Все)", StringComparison.Ordinal))
            categoryRaw = null; // no filter
        else if (string.Equals(categoryRaw, "(Без категории)", StringComparison.Ordinal))
            categoryRaw = string.Empty; // explicit empty category

        var q = string.IsNullOrWhiteSpace(query) ? null : Uri.EscapeDataString(query);
        var cat = string.IsNullOrWhiteSpace(categoryRaw) ? null : Uri.EscapeDataString(categoryRaw);
        var parts = new List<string> { "page=1", "size=50" };
        if (!string.IsNullOrEmpty(q)) parts.Add($"query={q}");
        if (!string.IsNullOrEmpty(cat)) parts.Add($"category={cat}");

        var url = "/api/products?" + string.Join("&", parts);

        var result = await client.GetFromJsonAsync<PagedResultDto<ProductDto>>(url, ct);
        var list = result?.Items ?? new List<ProductDto>();
        // Fallback client-side filter if server ignored category
        if (!string.IsNullOrWhiteSpace(categoryRaw))
        {
            list = list.Where(p => string.Equals(p.Category ?? string.Empty, categoryRaw, StringComparison.Ordinal)).ToList();
        }
        var items = list.Select(p =>
        {
            var name = TextEncodingHelper.Normalize(p.Name);
            var categoryValue = TextEncodingHelper.Normalize(p.Category);
            var skuFixed = TextEncodingHelper.Normalize(p.Sku);
            return new ProductModel
            {
                Id = p.Id,
                Name = name ?? string.Empty,
                Sku = skuFixed ?? p.Sku,
                Unit = string.Empty,
                Price = p.UnitPrice,
                Category = categoryValue ?? string.Empty
            };
        });
        return items;
    }

    public async Task<IEnumerable<string>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);

        var endpoints = new[]
        {
            "/api/products/categories",
            "/api/categories"
        };

        foreach (var endpoint in endpoints)
        {
            var items = await TryFetchCategoriesAsync(client, endpoint, ct);
            if (items.Count > 0)
            {
                return items
                    .Select(TextEncodingHelper.Normalize)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s)
                    .ToList();
            }
        }

        // Fallback: derive categories from products list when category endpoints fail
        try
        {
            var products = await SearchAsync(query: null, category: null, ct: ct);
            var derived = products
                .Select(p => TextEncodingHelper.Normalize(p.Category) ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToList();
            return derived;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ApiCatalogService] Fallback derive categories failed: {ex}");
            return Array.Empty<string>();
        }
    }

    private static async Task<List<string>> TryFetchCategoriesAsync(HttpClient client, string path, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            Debug.WriteLine($"[ApiCatalogService] GET {path} => {(int)response.StatusCode} {response.StatusCode}: {TrimForLog(body)}");

            if (!response.IsSuccessStatusCode)
            {
                return new List<string>();
            }

            var parsed = JsonSerializer.Deserialize<List<string>>(body, CategoriesSerializerOptions);
            return (parsed ?? new List<string>())
                .Select(TextEncodingHelper.Normalize)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ApiCatalogService] GET {path} failed: {ex}");
            return new List<string>();
        }
    }

    private static string TrimForLog(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        const int limit = 300;
        return value.Length <= limit ? value : value[..limit] + "...";
    }

    private class ProductBrief
    {
        public int Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public async Task<Dictionary<int, (string Sku, string Name)>> LookupAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var listIds = ids.Distinct().ToList();
        if (listIds.Count == 0) return new Dictionary<int, (string, string)>();
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);
        var qs = string.Join(',', listIds);
        var url = $"/api/products/lookup?ids={Uri.EscapeDataString(qs)}";
        var list = await client.GetFromJsonAsync<List<ProductBrief>>(url, ct) ?? new List<ProductBrief>();
        return list.ToDictionary(
            p => p.Id,
            p =>
            {
                var fixedName = TextEncodingHelper.Normalize(p.Name) ?? string.Empty;
                return (p.Sku, fixedName);
            });
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
