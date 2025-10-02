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
        var client = _httpClientFactory.CreateClient();
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
        return resp.IsSuccessStatusCode;
    }
}
