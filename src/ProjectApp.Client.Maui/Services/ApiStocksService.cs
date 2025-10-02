using System.Net.Http.Json;

namespace ProjectApp.Client.Maui.Services;

public class ApiStocksService : IStocksService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppSettings _settings;
    private readonly AuthService _auth;

    public ApiStocksService(IHttpClientFactory httpClientFactory, AppSettings settings, AuthService auth)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _auth = auth;
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
        var client = _httpClientFactory.CreateClient();
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);
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
                var body2 = await resp2.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException(string.IsNullOrWhiteSpace(body2) ? $"HTTP {(int)resp2.StatusCode} {resp2.StatusCode}" : body2);
            }
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(string.IsNullOrWhiteSpace(body) ? $"HTTP {(int)resp.StatusCode} {resp.StatusCode}" : body);
        }

        var list = await fetchAsync(url);
        return list.Select(d => new StockViewModel
        {
            ProductId = d.ProductId,
            Sku = d.Sku,
            Name = d.Name,
            Category = d.Category,
            Nd40Qty = d.Nd40Qty,
            Im40Qty = d.Im40Qty,
            TotalQty = d.TotalQty
        });
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
        var client = _httpClientFactory.CreateClient();
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
                var body2 = await resp2.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException(string.IsNullOrWhiteSpace(body2) ? $"HTTP {(int)resp2.StatusCode} {resp2.StatusCode}" : body2);
            }
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(string.IsNullOrWhiteSpace(body) ? $"HTTP {(int)resp.StatusCode} {resp.StatusCode}" : body);
        }

        var list = await fetchAsync(url);
        return list.Select(d => new BatchStockViewModel
        {
            ProductId = d.ProductId,
            Sku = d.Sku,
            Name = d.Name,
            Category = d.Category,
            Register = d.Register,
            Code = d.Code,
            Qty = d.Qty,
            UnitCost = d.UnitCost,
            CreatedAt = d.CreatedAt,
            Note = d.Note
        });
    }
}
