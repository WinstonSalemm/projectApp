using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.Services;

public class ApiClientsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppSettings _settings;
    private readonly AuthService _auth;

    public ApiClientsService(IHttpClientFactory httpClientFactory, AppSettings settings, AuthService auth)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _auth = auth;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);
        return client;
    }

    private class Paged<T>
    {
        public List<T> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int Size { get; set; }
    }

    private class ClientDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Inn { get; set; }
        public ClientType Type { get; set; }
        public string? OwnerUserName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public async Task<(IEnumerable<ClientListItem> items, int total)> ListAsync(string? q = null, ClientType? type = null, string? owner = null, int page = 1, int size = 50, CancellationToken ct = default)
    {
        var client = CreateClient();
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(q)) qs.Add($"q={Uri.EscapeDataString(q)}");
        if (type.HasValue) qs.Add($"type={(int)type.Value}");
        if (!string.IsNullOrWhiteSpace(owner)) qs.Add($"owner={Uri.EscapeDataString(owner)}");
        qs.Add($"page={page}");
        qs.Add($"size={size}");
        var url = "/api/clients" + (qs.Count > 0 ? ("?" + string.Join("&", qs)) : string.Empty);
        var resp = await client.GetFromJsonAsync<Paged<ClientDto>>(url, JsonOptions, ct) ?? new Paged<ClientDto>();
        return (resp.Items.Select(Map).ToList(), resp.Total);
    }

    public async Task<IEnumerable<ClientListItem>> GetCommissionAgentsAsync(CancellationToken ct = default)
    {
        var client = CreateClient();
        var list = await client.GetFromJsonAsync<List<ClientDto>>("/api/commissions/agents", JsonOptions, ct) ?? new List<ClientDto>();
        return list.Select(Map);
    }

    public async Task<ClientListItem?> GetAsync(int id, CancellationToken ct = default)
    {
        var client = CreateClient();
        var dto = await client.GetFromJsonAsync<ClientDto>($"/api/clients/{id}", JsonOptions, ct);
        return dto == null ? null : Map(dto);
    }

    public async Task<int> CreateAsync(ClientCreateDraft draft, CancellationToken ct = default)
    {
        var client = CreateClient();
        var body = new {
            name = draft.Name,
            phone = draft.Phone,
            inn = draft.Inn,
            type = draft.Type
        };
        var resp = await client.PostAsJsonAsync("/api/clients", body, ct);
        resp.EnsureSuccessStatusCode();
        var root = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("id", out var idProp) && idProp.TryGetInt32(out var id))
            return id;
        throw new InvalidOperationException("API вернул неожиданный ответ при создании клиента");
    }

    public async Task<bool> UpdateAsync(int id, ClientUpdateDraft draft, CancellationToken ct = default)
    {
        var client = CreateClient();
        var body = new {
            name = draft.Name,
            phone = draft.Phone,
            inn = draft.Inn,
            type = draft.Type
        };
        var resp = await client.PutAsJsonAsync($"/api/clients/{id}", body, ct);
        return resp.IsSuccessStatusCode;
    }

    private static ClientListItem Map(ClientDto dto) => new ClientListItem
    {
        Id = dto.Id,
        Name = dto.Name,
        Phone = dto.Phone,
        Inn = dto.Inn,
        Type = dto.Type,
        OwnerUserName = dto.OwnerUserName,
        CreatedAt = dto.CreatedAt
    };

    // ---- Client histories ----
    public class SaleBriefDto
    {
        public int Id { get; set; }
        public int? ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string PaymentType { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class ReturnBriefDto
    {
        public int Id { get; set; }
        public int? RefSaleId { get; set; }
        public int? ClientId { get; set; }
        public decimal Sum { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Reason { get; set; }
    }

    public enum DebtStatus { Open = 0, Paid = 1, Overdue = 2, Canceled = 3 }

    public class DebtListItem
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public int SaleId { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public DebtStatus Status { get; set; }
    }

    public async Task<IEnumerable<SaleBriefDto>> GetSalesAsync(int clientId, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var client = CreateClient();
        var qs = new List<string>();
        if (from.HasValue) qs.Add($"from={Uri.EscapeDataString(from.Value.ToString("o"))}");
        if (to.HasValue) qs.Add($"to={Uri.EscapeDataString(to.Value.ToString("o"))}");
        var url = $"/api/clients/{clientId}/sales" + (qs.Count > 0 ? ("?" + string.Join("&", qs)) : string.Empty);
        var list = await client.GetFromJsonAsync<List<SaleBriefDto>>(url, ct);
        return list ?? Enumerable.Empty<SaleBriefDto>();
    }

    public async Task<IEnumerable<ReturnBriefDto>> GetReturnsAsync(int clientId, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var client = CreateClient();
        var qs = new List<string>();
        if (from.HasValue) qs.Add($"from={Uri.EscapeDataString(from.Value.ToString("o"))}");
        if (to.HasValue) qs.Add($"to={Uri.EscapeDataString(to.Value.ToString("o"))}");
        var url = $"/api/clients/{clientId}/returns" + (qs.Count > 0 ? ("?" + string.Join("&", qs)) : string.Empty);
        var list = await client.GetFromJsonAsync<List<ReturnBriefDto>>(url, ct);
        return list ?? Enumerable.Empty<ReturnBriefDto>();
    }

    public async Task<IEnumerable<DebtListItem>> GetDebtsAsync(int clientId, DebtStatus? status = null, CancellationToken ct = default)
    {
        var client = CreateClient();
        var qs = new List<string>();
        if (status.HasValue) qs.Add($"status={(int)status.Value}");
        var url = $"/api/clients/{clientId}/debts" + (qs.Count > 0 ? ("?" + string.Join("&", qs)) : string.Empty);
        var list = await client.GetFromJsonAsync<List<DebtListItem>>(url, ct);
        return list ?? Enumerable.Empty<DebtListItem>();
    }

    // ---- Unregistered (anonymous) bucket ----
    public async Task<IEnumerable<SaleBriefDto>> GetUnregisteredSalesAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var client = CreateClient();
        var qs = new List<string>();
        if (from.HasValue) qs.Add($"from={Uri.EscapeDataString(from.Value.ToString("o"))}");
        if (to.HasValue) qs.Add($"to={Uri.EscapeDataString(to.Value.ToString("o"))}");
        var url = "/api/clients/unregistered/sales" + (qs.Count > 0 ? ("?" + string.Join("&", qs)) : string.Empty);
        var list = await client.GetFromJsonAsync<List<SaleBriefDto>>(url, ct);
        return list ?? Enumerable.Empty<SaleBriefDto>();
    }

    public async Task<IEnumerable<ReturnBriefDto>> GetUnregisteredReturnsAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var client = CreateClient();
        var qs = new List<string>();
        if (from.HasValue) qs.Add($"from={Uri.EscapeDataString(from.Value.ToString("o"))}");
        if (to.HasValue) qs.Add($"to={Uri.EscapeDataString(to.Value.ToString("o"))}");
        var url = "/api/clients/unregistered/returns" + (qs.Count > 0 ? ("?" + string.Join("&", qs)) : string.Empty);
        var list = await client.GetFromJsonAsync<List<ReturnBriefDto>>(url, ct);
        return list ?? Enumerable.Empty<ReturnBriefDto>();
    }
}

