using System.Net.Http.Json;
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

    private class SaleCreateItemDto
    {
        public int ProductId { get; set; }
        public decimal Qty { get; set; }
    }

    private class SaleCreateDto
    {
        public int? ClientId { get; set; }
        public string ClientName { get; set; } = "Quick Sale";
        public List<SaleCreateItemDto> Items { get; set; } = new();
        public string PaymentType { get; set; } = "CashWithReceipt";
        public List<string>? ReservationNotes { get; set; }
    }

    public async Task<bool> SubmitSaleAsync(SaleDraft draft, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient();
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);
        var dto = new SaleCreateDto
        {
            ClientId = null,
            ClientName = string.IsNullOrWhiteSpace(draft.ClientName) ? "Quick Sale" : draft.ClientName,
            Items = draft.Items.Select(i => new SaleCreateItemDto
            {
                ProductId = i.ProductId,
                Qty = (decimal)i.Qty
            }).ToList(),
            PaymentType = draft.PaymentType.ToString(),
            ReservationNotes = draft.ReservationNotes
        };

        var response = await client.PostAsJsonAsync("/api/sales", dto, ct);
        return (int)response.StatusCode == 201;
    }
}
