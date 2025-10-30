using System.Net.Http.Json;

namespace ProjectApp.Client.Maui.Services;

public class ApiContractsService : IContractsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppSettings _settings;
    private readonly AuthService _auth;

    public ApiContractsService(IHttpClientFactory httpClientFactory, AppSettings settings, AuthService auth)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _auth = auth;
    }

    public async Task<IEnumerable<ContractListItem>> GetContractsAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);

        var resp = await client.GetAsync("/api/contracts", ct);
        if (!resp.IsSuccessStatusCode)
        {
            var pd = await TryReadProblem(resp, ct);
            throw new InvalidOperationException(pd ?? $"HTTP {(int)resp.StatusCode} {resp.StatusCode}");
        }
        
        var list = await resp.Content.ReadFromJsonAsync<List<ContractDto>>(cancellationToken: ct);
        try { System.Diagnostics.Debug.WriteLine($"[ApiContractsService] Loaded {list?.Count ?? 0} contracts from API"); } catch { }
        if (list == null) return Array.Empty<ContractListItem>();
        
        var mapped = list.Select(dto => new ContractListItem
        {
            Id = dto.Id,
            Type = dto.Type,
            ContractNumber = dto.ContractNumber,
            ClientId = dto.ClientId,
            OrgName = dto.OrgName,
            Inn = dto.Inn,
            Phone = dto.Phone,
            Status = dto.Status,
            CreatedAt = dto.CreatedAt,
            CreatedBy = dto.CreatedBy,
            Note = dto.Note,
            Description = dto.Description,
            TotalAmount = dto.TotalAmount,
            PaidAmount = dto.PaidAmount,
            ShippedAmount = dto.ShippedAmount,
            PaidPercent = dto.PaidPercent,
            ShippedPercent = dto.ShippedPercent,
            BalanceDue = dto.BalanceDue,
            ItemsCount = dto.Items?.Count ?? 0
        }).ToList();
        try
        {
            var typeStats = mapped.GroupBy(m => m.Type ?? "<null>").Select(g => $"{g.Key}:{g.Count()}");
            System.Diagnostics.Debug.WriteLine($"[ApiContractsService] Types: {string.Join(", ", typeStats)}");
        }
        catch { }
        return mapped;
    }

    public async Task<ContractDetail?> GetAsync(int id, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);

        var resp = await client.GetAsync($"/api/contracts/{id}", ct);
        if (!resp.IsSuccessStatusCode)
        {
            var pd = await TryReadProblem(resp, ct);
            throw new InvalidOperationException(pd ?? $"HTTP {(int)resp.StatusCode} {resp.StatusCode}");
        }
        var dto = await resp.Content.ReadFromJsonAsync<ContractDto>(cancellationToken: ct);
        if (dto == null) return null;
        return new ContractDetail
        {
            Id = dto.Id,
            OrgName = dto.OrgName,
            Inn = dto.Inn,
            Phone = dto.Phone,
            Status = dto.Status,
            CreatedAt = dto.CreatedAt,
            Note = dto.Note,
            Items = dto.Items.Select(i => new ContractItemDraft
            {
                ProductId = i.ProductId,
                Name = i.Name,
                Unit = i.Unit,
                Qty = i.Qty,
                UnitPrice = i.UnitPrice
            }).ToList()
        };
    }

    public async Task<bool> UpdateAsync(int id, ContractCreateDraft draft, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);

        var dto = new ContractCreateDto
        {
            Type = draft.Type,
            ContractNumber = draft.ContractNumber,
            ClientId = draft.ClientId,
            OrgName = draft.OrgName,
            Inn = draft.Inn,
            Phone = draft.Phone,
            Description = draft.Description,
            TotalAmount = draft.TotalAmount,
            Note = draft.Note,
            Items = draft.Items.Select(i => new ContractItemDto
            {
                ProductId = i.ProductId,
                Name = i.Name,
                Unit = i.Unit,
                Qty = i.Qty,
                UnitPrice = i.UnitPrice
            }).ToList()
        };
        try { System.Diagnostics.Debug.WriteLine($"[ApiContractsService] Create: sending Type='{dto.Type}', Items={dto.Items.Count}, TotalAmount={(dto.TotalAmount?.ToString() ?? "null")}" ); } catch { }

        var resp = await client.PutAsJsonAsync($"/api/contracts/{id}", dto, ct);
        if (resp.IsSuccessStatusCode) return true;
        var pd = await TryReadProblem(resp, ct);
        throw new InvalidOperationException(pd ?? $"HTTP {(int)resp.StatusCode} {resp.StatusCode}");
    }

    public class ContractItemDto
    {
        public int? ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = "шт";
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
    }
    public class ContractDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string ContractNumber { get; set; } = string.Empty;
        public int? ClientId { get; set; }
        public string OrgName { get; set; } = string.Empty;
        public string? Inn { get; set; }
        public string? Phone { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? Note { get; set; }
        public string? Description { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal ShippedAmount { get; set; }
        public decimal PaidPercent { get; set; }
        public decimal ShippedPercent { get; set; }
        public decimal BalanceDue { get; set; }
        public List<ContractItemDto> Items { get; set; } = new();
    }
    private class ContractCreateDto
    {
        public string Type { get; set; } = "Closed"; // Closed | Open
        public string? ContractNumber { get; set; }
        public int? ClientId { get; set; }
        public string OrgName { get; set; } = string.Empty;
        public string? Inn { get; set; }
        public string? Phone { get; set; }
        public string? Description { get; set; }
        public decimal? TotalAmount { get; set; }
        public string? Note { get; set; }
        public List<ContractItemDto> Items { get; set; } = new();
    }
    private class ContractUpdateStatusDto
    {
        public string Status { get; set; } = "Signed";
    }
    private class ProblemDetails
    {
        public string? Type { get; set; }
        public string? Title { get; set; }
        public int? Status { get; set; }
        public string? Detail { get; set; }
        public string? Instance { get; set; }
    }

    public async Task<IEnumerable<ContractListItem>> ListAsync(string? status = null, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);

        var url = "/api/contracts" + (string.IsNullOrWhiteSpace(status) ? string.Empty : $"?status={Uri.EscapeDataString(status)}");
        var resp = await client.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var pd = await TryReadProblem(resp, ct);
            throw new InvalidOperationException(pd ?? $"HTTP {(int)resp.StatusCode} {resp.StatusCode}");
        }
        var list = await resp.Content.ReadFromJsonAsync<List<ContractDto>>(cancellationToken: ct) ?? new();
        return list.Select(c => new ContractListItem
        {
            Id = c.Id,
            OrgName = c.OrgName,
            Inn = c.Inn,
            Phone = c.Phone,
            Status = c.Status,
            CreatedAt = c.CreatedAt,
            Note = c.Note
        });
    }

    public async Task<bool> CreateAsync(ContractCreateDraft draft, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);

        var dto = new ContractCreateDto
        {
            Type = draft.Type,
            ContractNumber = draft.ContractNumber,
            ClientId = draft.ClientId,
            OrgName = draft.OrgName,
            Inn = draft.Inn,
            Phone = draft.Phone,
            Description = draft.Description,
            TotalAmount = draft.TotalAmount,
            Note = draft.Note,
            Items = draft.Items.Select(i => new ContractItemDto
            {
                ProductId = i.ProductId,
                Name = i.Name,
                Unit = i.Unit,
                Qty = i.Qty,
                UnitPrice = i.UnitPrice
            }).ToList()
        };

        try 
        { 
            var json = System.Text.Json.JsonSerializer.Serialize(dto);
            System.Diagnostics.Debug.WriteLine($"[ApiContractsService] Create(POST) payload: {json}");
        } 
        catch { }
        var resp = await client.PostAsJsonAsync("/api/contracts", dto, ct);
        try { System.Diagnostics.Debug.WriteLine($"[ApiContractsService] Create(POST): status={(int)resp.StatusCode} {resp.StatusCode}"); } catch { }
        if ((int)resp.StatusCode == 201) return true;
        var pd = await TryReadProblem(resp, ct);
        throw new InvalidOperationException(pd ?? $"HTTP {(int)resp.StatusCode} {resp.StatusCode}");
    }

    public async Task<bool> UpdateStatusAsync(int id, string status, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);

        var dto = new ContractUpdateStatusDto { Status = status };
        var resp = await client.PutAsJsonAsync($"/api/contracts/{id}/status", dto, ct);
        if (resp.IsSuccessStatusCode) return true;
        var pd = await TryReadProblem(resp, ct);
        throw new InvalidOperationException(pd ?? $"HTTP {(int)resp.StatusCode} {resp.StatusCode}");
    }

    public async Task<IEnumerable<ContractDto>> GetContractsAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);

        var resp = await client.GetAsync("/api/contracts", ct);
        if (!resp.IsSuccessStatusCode)
        {
            var pd = await TryReadProblem(resp, ct);
            throw new InvalidOperationException(pd ?? $"HTTP {(int)resp.StatusCode} {resp.StatusCode}");
        }
        
        var list = await resp.Content.ReadFromJsonAsync<List<ContractDto>>(cancellationToken: ct) ?? new();
        
        // Client-side date filtering
        var filtered = list.AsEnumerable();
        if (from.HasValue)
            filtered = filtered.Where(c => c.CreatedAt >= from.Value);
        if (to.HasValue)
            filtered = filtered.Where(c => c.CreatedAt < to.Value);
            
        return filtered;
    }

    private static async Task<string?> TryReadProblem(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var pd = await resp.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: ct);
            if (pd != null && !string.IsNullOrWhiteSpace(pd.Detail)) return pd.Detail;
            var txt = await resp.Content.ReadAsStringAsync(ct);
            return string.IsNullOrWhiteSpace(txt) ? null : txt;
        }
        catch { return null; }
    }
}

