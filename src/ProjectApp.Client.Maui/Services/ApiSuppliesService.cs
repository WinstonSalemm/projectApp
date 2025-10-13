using System.Net.Http.Json;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.Services;

public class ApiSuppliesService : ISuppliesService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppSettings _settings;
    private readonly AuthService _auth;

    public ApiSuppliesService(IHttpClientFactory httpClientFactory, AppSettings settings, AuthService auth)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _auth = auth;
    }

    private class SupplyLineDto
    {
        public int ProductId { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitCost { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? Note { get; set; }
    }
    private class SupplyCreateDto
    {
        public List<SupplyLineDto> Items { get; set; } = new();
    }

    private class SupplyTransferItemDto
    {
        public int ProductId { get; set; }
        public decimal Qty { get; set; }
    }
    private class SupplyTransferDto
    {
        public List<SupplyTransferItemDto> Items { get; set; } = new();
    }

    private class ProblemDetails
    {
        public string? Type { get; set; }
        public string? Title { get; set; }
        public int? Status { get; set; }
        public string? Detail { get; set; }
        public string? Instance { get; set; }
    }

    public async Task<bool> CreateSupplyAsync(SupplyDraft draft, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient();
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);

        var dto = new SupplyCreateDto
        {
            Items = draft.Items.Select(i => new SupplyLineDto
            {
                ProductId = i.ProductId,
                Qty = i.Qty,
                UnitCost = i.UnitCost,
                Code = i.Code ?? string.Empty,
                Note = i.Note
            }).ToList()
        };
        var resp = await client.PostAsJsonAsync("/api/supplies", dto, ct);
        if ((int)resp.StatusCode == 201) return true;

        try
        {
            var pd = await resp.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: ct);
            var msg = pd?.Detail;
            if (string.IsNullOrWhiteSpace(msg)) msg = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(msg) ? $"HTTP {(int)resp.StatusCode} {resp.StatusCode}" : msg);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(ex.Message);
        }
    }

    public async Task<bool> TransferToIm40Async(string code, List<SupplyTransferItem> items, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient();
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);

        var dto = new SupplyTransferDto
        {
            Items = items.Select(i => new SupplyTransferItemDto { ProductId = i.ProductId, Qty = i.Qty }).ToList()
        };
        var resp = await client.PostAsJsonAsync($"/api/supplies/{Uri.EscapeDataString(code)}/to-im40", dto, ct);
        if (resp.IsSuccessStatusCode) return true;
        try
        {
            var pd = await resp.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: ct);
            var msg = pd?.Detail;
            if (string.IsNullOrWhiteSpace(msg)) msg = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(msg) ? $"HTTP {(int)resp.StatusCode} {resp.StatusCode}" : msg);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(ex.Message);
        }
    }

    // ---- History listing ----
    public class SupplyBatchDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string Register { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public decimal UnitCost { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Code { get; set; }
        public string? Note { get; set; }
    }

    public async Task<IEnumerable<SupplyBatchDto>> QueryAsync(string? code = null, int? productId = null, string? register = null, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient();
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);

        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(code)) qs.Add($"code={Uri.EscapeDataString(code)}");
        if (productId.HasValue) qs.Add($"productId={productId.Value}");
        if (!string.IsNullOrWhiteSpace(register)) qs.Add($"register={Uri.EscapeDataString(register)}");
        var url = "/api/supplies" + (qs.Count > 0 ? ("?" + string.Join("&", qs)) : string.Empty);
        var list = await client.GetFromJsonAsync<List<SupplyBatchDto>>(url, ct);
        return list ?? Enumerable.Empty<SupplyBatchDto>();
    }
}

