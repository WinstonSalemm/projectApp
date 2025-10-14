using System;
using System.Collections.Generic;
using System.Net.Http.Json;

namespace ProjectApp.Client.Maui.Services;

    public class ApiReturnsService : IReturnsService
    {
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppSettings _settings;
    private readonly AuthService _auth;

    public ApiReturnsService(IHttpClientFactory httpClientFactory, AppSettings settings, AuthService auth)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _auth = auth;
    }

    private sealed class ProblemDetails
    {
        public string? Type { get; set; }
        public string? Title { get; set; }
        public int? Status { get; set; }
        public string? Detail { get; set; }
        public string? Instance { get; set; }
    }

    private class ReturnItemCreateDto
    {
        public int SaleItemId { get; set; }
        public decimal Qty { get; set; }
    }

    private class ReturnCreateDto
    {
        public int RefSaleId { get; set; }
        public int? ClientId { get; set; }
        public string? Reason { get; set; }
        public List<ReturnItemCreateDto>? Items { get; set; }
    }

    public async Task<bool> CreateReturnAsync(ReturnDraft draft, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);

        var dto = new ReturnCreateDto
        {
            RefSaleId = draft.RefSaleId,
            ClientId = draft.ClientId,
            Reason = draft.Reason,
            Items = draft.Items?.Select(i => new ReturnItemCreateDto { SaleItemId = i.SaleItemId, Qty = i.Qty }).ToList()
        };

        var resp = await client.PostAsJsonAsync("/api/returns", dto, ct);
        if (resp.IsSuccessStatusCode) return true;

        try
        {
            var problem = await resp.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: ct);
            var msg = problem?.Detail;
            if (string.IsNullOrWhiteSpace(msg)) msg = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(msg) ? $"HTTP {(int)resp.StatusCode} {resp.StatusCode}" : msg);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(ex.Message);
        }
    }
    public class ReturnItemDto
    {
        public int SaleItemId { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
    }
    public class ReturnDto
    {
        public int Id { get; set; }
        public int RefSaleId { get; set; }
        public int? ClientId { get; set; }
        public decimal Sum { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Reason { get; set; }
        public List<ReturnItemDto> Items { get; set; } = new();
    }

    public async Task<IEnumerable<ReturnDto>> QueryAsync(int? refSaleId = null, int? clientId = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);

        var qs = new List<string>();
        if (refSaleId.HasValue) qs.Add($"refSaleId={refSaleId.Value}");
        if (clientId.HasValue) qs.Add($"clientId={clientId.Value}");
        if (from.HasValue) qs.Add($"dateFrom={Uri.EscapeDataString(from.Value.ToString("o"))}");
        if (to.HasValue) qs.Add($"dateTo={Uri.EscapeDataString(to.Value.ToString("o"))}");
        var url = "/api/returns" + (qs.Count > 0 ? ("?" + string.Join("&", qs)) : string.Empty);
        var list = await client.GetFromJsonAsync<List<ReturnDto>>(url, ct);
        return list ?? Enumerable.Empty<ReturnDto>();
    }

    public async Task<IEnumerable<ReturnDto>> GetBySaleAsync(int saleId, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);
        var url = $"/api/sales/{saleId}/returns";
        var list = await client.GetFromJsonAsync<List<ReturnDto>>(url, ct);
        return list ?? Enumerable.Empty<ReturnDto>();
    }

    public async Task<bool> CancelBySaleAsync(int saleId, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);
        var resp = await client.PostAsync($"/api/sales/{saleId}/return/cancel", content: null, ct);
        return resp.IsSuccessStatusCode;
    }
}

