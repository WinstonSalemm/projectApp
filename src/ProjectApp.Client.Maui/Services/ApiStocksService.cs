using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace ProjectApp.Client.Maui.Services;

public class ApiStocksService : IStocksService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppSettings _settings;
    private readonly AuthService _auth;
    private readonly ILogger<ApiStocksService> _logger;

    public ApiStocksService(IHttpClientFactory httpClientFactory, AppSettings settings, AuthService auth, ILogger<ApiStocksService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _auth = auth;
        _logger = logger;
    }

    private class StockViewDto
    {
        public int ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Nd40Qty { get; set; }
        public decimal Im40Qty { get; set; }
        public decimal TotalQty { get; set; }
    }

    public async Task<IEnumerable<StockViewModel>> GetStocksAsync(string? query = null, string? category = null, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
            var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
            client.BaseAddress = new Uri(baseUrl);
            _auth.ConfigureClient(client);
            
            _logger.LogInformation("[ApiStocksService] GetStocksAsync: baseUrl={BaseUrl}, query={Query}, category={Category}", baseUrl, query, category);
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(query)) parts.Add($"query={Uri.EscapeDataString(query)}");
        if (!string.IsNullOrWhiteSpace(category)) parts.Add($"category={Uri.EscapeDataString(category)}");
        var qs = parts.Count > 0 ? ("?" + string.Join("&", parts)) : string.Empty;
        var url = "/api/stocks" + qs;

        async Task<List<StockViewDto>> fetchAsync(string path)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, path);
            using var resp = await client.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                var data = await resp.Content.ReadFromJsonAsync<List<StockViewDto>>(cancellationToken: ct);
                return data ?? new();
            }
            // On 404, also try without '/api' prefix
            if ((int)resp.StatusCode == 404)
            {
                var alt = "/stocks" + qs;
                using var req2 = new HttpRequestMessage(HttpMethod.Get, alt);
                using var resp2 = await client.SendAsync(req2, ct);
                if (resp2.IsSuccessStatusCode)
                {
                    var data2 = await resp2.Content.ReadFromJsonAsync<List<StockViewDto>>(cancellationToken: ct);
                    return data2 ?? new();
                }
                // Fallback: return empty on failure
                return new();
            }
            // Non-success: return empty to avoid crashing the UI
            return new();
        }

            var list = await fetchAsync(url);
            _logger.LogInformation("[ApiStocksService] GetStocksAsync: received {Count} stocks", list.Count);
            return list.Select(d => new StockViewModel
            {
                ProductId = d.ProductId,
                Sku = d.Sku,
                Name = ProjectApp.Client.Maui.Utils.TextEncodingHelper.Normalize(d.Name) ?? string.Empty,
                Category = ProjectApp.Client.Maui.Utils.TextEncodingHelper.Normalize(d.Category) ?? string.Empty,
                Nd40Qty = d.Nd40Qty,
                Im40Qty = d.Im40Qty,
                TotalQty = d.TotalQty
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ApiStocksService] GetStocksAsync failed: baseUrl={BaseUrl}, query={Query}, category={Category}", _settings.ApiBaseUrl, query, category);
            throw;
        }
    }

    private class BatchStockViewDto
    {
        public int ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Register { get; set; } = string.Empty;
        public string? Code { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitCost { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Note { get; set; }
    }

    public async Task<IEnumerable<BatchStockViewModel>> GetBatchesAsync(string? query = null, string? category = null, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(query)) parts.Add($"query={Uri.EscapeDataString(query)}");
        if (!string.IsNullOrWhiteSpace(category)) parts.Add($"category={Uri.EscapeDataString(category)}");
        var qs = parts.Count > 0 ? ("?" + string.Join("&", parts)) : string.Empty;
        var url = "/api/stocks/batches" + qs;

        async Task<List<BatchStockViewDto>> fetchAsync(string path)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, path);
            using var resp = await client.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                var data = await resp.Content.ReadFromJsonAsync<List<BatchStockViewDto>>(cancellationToken: ct);
                return data ?? new();
            }
            if ((int)resp.StatusCode == 404 && _settings.ApiBaseUrl?.TrimEnd('/')?.EndsWith("/api", StringComparison.OrdinalIgnoreCase) == true)
            {
                var alt = "/stocks/batches" + qs;
                using var req2 = new HttpRequestMessage(HttpMethod.Get, alt);
                using var resp2 = await client.SendAsync(req2, ct);
                if (resp2.IsSuccessStatusCode)
                {
                    var data2 = await resp2.Content.ReadFromJsonAsync<List<BatchStockViewDto>>(cancellationToken: ct);
                    return data2 ?? new();
                }
                return new();
            }
            return new();
        }

        var list = await fetchAsync(url);
        return list.Select(d => new BatchStockViewModel
        {
            ProductId = d.ProductId,
            Sku = d.Sku,
            Name = ProjectApp.Client.Maui.Utils.TextEncodingHelper.Normalize(d.Name) ?? string.Empty,
            Category = ProjectApp.Client.Maui.Utils.TextEncodingHelper.Normalize(d.Category) ?? string.Empty,
            Register = d.Register,
            Code = d.Code,
            Qty = d.Qty,
            UnitCost = d.UnitCost,
            CreatedAt = d.CreatedAt,
            Note = d.Note
        });
    }

    private class AvailabilityDto
    {
        public string Key { get; set; } = string.Empty; // productId as string
        public decimal TotalQty { get; set; }
        public decimal Im40Qty { get; set; }
        public decimal Nd40Qty { get; set; }
    }

    // Open endpoint (no auth) used as a fallback for quick sale when secured stocks endpoint is unavailable
    public async Task<Dictionary<int, (decimal Total, decimal Im40, decimal Nd40)>> GetAvailabilityByProductIdsAsync(IEnumerable<int> productIds, CancellationToken ct = default)
    {
        var ids = productIds?.Distinct().ToList() ?? new List<int>();
        if (ids.Count == 0) return new();
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        // This endpoint is open; auth header not required but harmless
        _auth.ConfigureClient(client);
        var qs = Uri.EscapeDataString(string.Join(',', ids));
        var url = $"/api/stock/available/by-product-ids?ids={qs}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var resp = await client.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            return new();
        }
        var list = await resp.Content.ReadFromJsonAsync<List<AvailabilityDto>>(cancellationToken: ct) ?? new();
        var dict = new Dictionary<int, (decimal Total, decimal Im40, decimal Nd40)>();
        foreach (var a in list)
        {
            if (int.TryParse(a.Key, out var pid))
            {
                dict[pid] = (a.TotalQty, a.Im40Qty, a.Nd40Qty);
            }
        }
        return dict;
    }

    // Another open endpoint (key = SKU)
    public async Task<Dictionary<string, (decimal Total, decimal Im40, decimal Nd40)>> GetAvailabilityBySkusAsync(IEnumerable<string> skus, CancellationToken ct = default)
    {
        var list = skus?.Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s.Trim().ToUpperInvariant())
                        .Distinct()
                        .ToList() ?? new List<string>();
        if (list.Count == 0) return new();
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);
        var qs = Uri.EscapeDataString(string.Join(',', list));
        var url = $"/api/stock/available?skus={qs}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var resp = await client.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return new();
        var arr = await resp.Content.ReadFromJsonAsync<List<AvailabilityDto>>(cancellationToken: ct) ?? new();
        var dict = new Dictionary<string, (decimal Total, decimal Im40, decimal Nd40)>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in arr)
        {
            if (!string.IsNullOrWhiteSpace(a.Key))
                dict[a.Key] = (a.TotalQty, a.Im40Qty, a.Nd40Qty);
        }
        return dict;
    }
}

