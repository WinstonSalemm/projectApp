using System.Net.Http.Json;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.Services;

public class ApiSalesService : ISalesService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppSettings _settings;
    private readonly AuthService _auth;

    public ApiSalesService(IHttpClientFactory httpClientFactory, AppSettings settings, AuthService auth)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _auth = auth;
    }

    public async Task<bool> UploadSalePhotoAsync(int saleId, Stream photoStream, string fileName, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);

        using var form = new MultipartFormDataContent();
        var sc = new StreamContent(photoStream);
        sc.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(sc, "file", fileName);
        var resp = await client.PostAsync($"/api/sales/{saleId}/photo", form, ct);
        return resp.IsSuccessStatusCode;
    }

    private class SaleCreateItemDto
    {
        public int ProductId { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
    }

    private class SaleCreateDto
    {
        public int? ClientId { get; set; }
        public string ClientName { get; set; } = "Quick Sale";
        public List<SaleCreateItemDto> Items { get; set; } = new();
        public string PaymentType { get; set; } = "CashWithReceipt";
        public List<string>? ReservationNotes { get; set; }
        public bool? NotifyHold { get; set; }
    }

    private class ProblemDetails
    {
        public string? Type { get; set; }
        public string? Title { get; set; }
        public int? Status { get; set; }
        public string? Detail { get; set; }
        public string? Instance { get; set; }
    }

    public async Task<SalesResult> SubmitSaleAsync(SaleDraft draft, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);
        var dto = new SaleCreateDto
        {
            ClientId = draft.ClientId,
            ClientName = string.IsNullOrWhiteSpace(draft.ClientName) ? "Quick Sale" : draft.ClientName,
            Items = draft.Items.Select(i => new SaleCreateItemDto
            {
                ProductId = i.ProductId,
                Qty = (decimal)i.Qty,
                UnitPrice = i.UnitPrice
            }).ToList(),
            PaymentType = draft.PaymentType.ToString(),
            ReservationNotes = draft.ReservationNotes,
            NotifyHold = draft.NotifyHold
        };

        var response = await client.PostAsJsonAsync("/api/sales", dto, ct);
        if ((int)response.StatusCode == 201)
        {
            // try parse created sale to get Id
            try
            {
                var created = await response.Content.ReadFromJsonAsync<SaleDetailsDto>(cancellationToken: ct);
                return SalesResult.Ok(created?.Id);
            }
            catch
            {
                return SalesResult.Ok(null);
            }
        }

        try
        {
            // Try read problem+json
            var pd = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: ct);
            var msg = pd?.Detail;
            if (string.IsNullOrWhiteSpace(msg))
            {
                // fallback to raw text
                msg = await response.Content.ReadAsStringAsync(ct);
            }
            return SalesResult.Fail(msg);
        }
        catch
        {
            string body = string.Empty;
            try { body = await response.Content.ReadAsStringAsync(ct); } catch { }
            return SalesResult.Fail(string.IsNullOrWhiteSpace(body) ? $"HTTP {(int)response.StatusCode} {response.StatusCode}" : body);
        }
    }

    // ---- History listing ----
    public class SaleDto
    {
        public int Id { get; set; }
        public int? ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string PaymentType { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    public async Task<IEnumerable<SaleDto>> GetSalesAsync(DateTime? from = null, DateTime? to = null, string? createdBy = null, string? paymentType = null, int? clientId = null, bool all = false, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);

        var qs = new List<string>();
        if (from.HasValue) qs.Add($"dateFrom={Uri.EscapeDataString(from.Value.ToString("o"))}");
        if (to.HasValue) qs.Add($"dateTo={Uri.EscapeDataString(to.Value.ToString("o"))}");
        if (!string.IsNullOrWhiteSpace(createdBy)) qs.Add($"createdBy={Uri.EscapeDataString(createdBy)}");
        if (!string.IsNullOrWhiteSpace(paymentType)) qs.Add($"paymentType={Uri.EscapeDataString(paymentType)}");
        if (clientId.HasValue) qs.Add($"clientId={clientId.Value}");
        if (all) qs.Add("all=true");
        var url = "/api/sales" + (qs.Count > 0 ? ("?" + string.Join("&", qs)) : string.Empty);
        var resp = await client.GetFromJsonAsync<List<SaleDto>>(url, ct);
        return resp ?? Enumerable.Empty<SaleDto>();
    }

    // ---- Sale details ----
    public class SaleItemDetailsDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
    public class SaleDetailsDto
    {
        public int Id { get; set; }
        public int? ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string PaymentType { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public List<SaleItemDetailsDto> Items { get; set; } = new();
    }

    public async Task<SaleDetailsDto?> GetSaleByIdAsync(int id, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);
        var url = $"/api/sales/{id}";
        return await client.GetFromJsonAsync<SaleDetailsDto>(url, ct);
    }
}

