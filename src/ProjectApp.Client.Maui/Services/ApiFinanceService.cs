using System.Net.Http.Json;

namespace ProjectApp.Client.Maui.Services;

public class ApiFinanceService : IFinanceService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppSettings _settings;
    private readonly AuthService _auth;

    public ApiFinanceService(IHttpClientFactory httpClientFactory, AppSettings settings, AuthService auth)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _auth = auth;
    }

    private HttpClient Create()
    {
        var c = _httpClientFactory.CreateClient();
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        c.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(c);
        return c;
    }

    private static string BuildQs((string key, string? val)[] parts)
    {
        var list = new List<string>();
        foreach (var p in parts)
        {
            if (!string.IsNullOrWhiteSpace(p.val)) list.Add($"{p.key}={Uri.EscapeDataString(p.val!)}");
        }
        return list.Count > 0 ? ("?" + string.Join("&", list)) : string.Empty;
    }

    public async Task<string> GetSummaryJsonAsync(DateTime? from, DateTime? to, string? bucketBy = null, string? groupBy = null, CancellationToken ct = default)
    {
        using var c = Create();
        var qs = BuildQs(new[]
        {
            ("from", from?.ToString("O")),
            ("to", to?.ToString("O")),
            ("bucketBy", bucketBy),
            ("groupBy", groupBy)
        });
        return await c.GetStringAsync($"/api/finance/summary{qs}", ct);
    }

    public async Task<string> GetKpiJsonAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        using var c = Create();
        var qs = BuildQs(new[] { ("from", from?.ToString("O")), ("to", to?.ToString("O")) });
        return await c.GetStringAsync($"/api/finance/kpi{qs}", ct);
    }

    public async Task<string> GetCashFlowJsonAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        using var c = Create();
        var qs = BuildQs(new[] { ("from", from?.ToString("O")), ("to", to?.ToString("O")) });
        return await c.GetStringAsync($"/api/finance/cashflow{qs}", ct);
    }

    public async Task<string> GetAbcJsonAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        using var c = Create();
        var qs = BuildQs(new[] { ("from", from?.ToString("O")), ("to", to?.ToString("O")) });
        return await c.GetStringAsync($"/api/finance/abc{qs}", ct);
    }

    public async Task<string> GetXyzJsonAsync(DateTime? from, DateTime? to, string bucket = "month", CancellationToken ct = default)
    {
        using var c = Create();
        var qs = BuildQs(new[] { ("from", from?.ToString("O")), ("to", to?.ToString("O")), ("bucket", bucket) });
        return await c.GetStringAsync($"/api/finance/xyz{qs}", ct);
    }

    public async Task<string> GetTrendsJsonAsync(DateTime? from, DateTime? to, string metric = "revenue", string interval = "month", CancellationToken ct = default)
    {
        using var c = Create();
        var qs = BuildQs(new[] { ("from", from?.ToString("O")), ("to", to?.ToString("O")), ("metric", metric), ("interval", interval) });
        return await c.GetStringAsync($"/api/finance/trends{qs}", ct);
    }

    public async Task<string> GetTaxesBreakdownJsonAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        using var c = Create();
        var qs = BuildQs(new[] { ("from", from?.ToString("O")), ("to", to?.ToString("O")) });
        return await c.GetStringAsync($"/api/finance/taxes/breakdown{qs}", ct);
    }

    public async Task<string> GetClientsJsonAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        using var c = Create();
        var qs = BuildQs(new[] { ("from", from?.ToString("O")), ("to", to?.ToString("O")) });
        return await c.GetStringAsync($"/api/finance/clients{qs}", ct);
    }

    public async Task<string> GetAlertsPreviewJsonAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        using var c = Create();
        var qs = BuildQs(new[] { ("from", from?.ToString("O")), ("to", to?.ToString("O")) });
        return await c.GetStringAsync($"/api/finance/alerts/preview{qs}", ct);
    }
}

