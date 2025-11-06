using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProjectApp.Client.Maui.Services;

public class ApiCostingPreviewService
{
    private readonly HttpClient _http;

    public ApiCostingPreviewService(IHttpClientFactory httpClientFactory)
    {
        _http = httpClientFactory.CreateClient(HttpClientNames.Api);
    }

    public async Task<CostingPreviewDto> PreviewAsync(int supplyId, CostingConfigDto cfg, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"/api/costing/preview/{supplyId}", cfg, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = string.Empty;
                try { body = await resp.Content.ReadAsStringAsync(ct); } catch { /* ignore */ }
                return new CostingPreviewDto
                {
                    Rows = new List<CostingRowDto>(),
                    Warnings = new[] { $"Server error {(int)resp.StatusCode} {resp.ReasonPhrase}: {Truncate(body, 300)}" }
                };
            }

            var dto = await resp.Content.ReadFromJsonAsync<CostingPreviewDto>(cancellationToken: ct);
            return dto ?? new CostingPreviewDto { Rows = new List<CostingRowDto>() };
        }
        catch (Exception ex)
        {
            return new CostingPreviewDto
            {
                Rows = new List<CostingRowDto>(),
                Warnings = new[] { $"Network error: {ex.Message}" }
            };
        }
    }

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) ? value : (value.Length <= max ? value : value.Substring(0, max) + "...");
}

public sealed class CostingPreviewDto
{
    public List<CostingRowDto> Rows { get; init; } = new();
    public decimal TotalQty { get; init; }
    public decimal TotalBaseSumUzs { get; init; }
    public string[] Warnings { get; init; } = System.Array.Empty<string>();
}

public sealed class CostingRowDto
{
    public string SkuOrName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal BasePriceUzs { get; init; }
    public decimal LineBaseTotalUzs { get; init; }
    public decimal CustomsUzsPerUnit { get; init; }
    public decimal LoadingUzsPerUnit { get; init; }
    public decimal LogisticsUzsPerUnit { get; init; }
    public decimal WarehouseUzsPerUnit { get; init; }
    public decimal DeclarationUzsPerUnit { get; init; }
    public decimal CertificationUzsPerUnit { get; init; }
    public decimal McsUzsPerUnit { get; init; }
    public decimal DeviationUzsPerUnit { get; init; }
    public decimal CostPerUnitUzs { get; init; }
    public decimal TradePriceUzs { get; init; }
    public decimal VatUzs { get; init; }
    public decimal PriceWithVatUzs { get; init; }
    public decimal ProfitPerUnitUzs { get; init; }
    public decimal ProfitTaxUzs { get; init; }
    public decimal NetProfitUzs { get; init; }
}

public sealed class CostingConfigDto
{
    public decimal RubToUzs { get; init; }
    public decimal UsdToUzs { get; init; }
    public decimal CustomsFixedUzs { get; init; }
    public decimal LoadingTotalUzs { get; init; }
    public decimal LogisticsPct { get; init; }
    public decimal WarehousePct { get; init; }
    public decimal DeclarationPct { get; init; }
    public decimal CertificationPct { get; init; }
    public decimal McsPct { get; init; }
    public decimal DeviationPct { get; init; }
    public decimal TradeMarkupPct { get; init; }
    public decimal VatPct { get; init; }
    public decimal ProfitTaxPct { get; init; }
}
