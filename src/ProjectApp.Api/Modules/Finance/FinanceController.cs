using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectApp.Api.Modules.Finance.Dtos;
using ProjectApp.Api.Modules.Finance.CashFlow;
using ProjectApp.Api.Modules.Finance.Forecast;
using ProjectApp.Api.Modules.Finance.Analysis;
using ProjectApp.Api.Modules.Finance.Trends;
using ProjectApp.Api.Modules.Finance.Ratios;
using ProjectApp.Api.Modules.Finance.Taxes;
using ProjectApp.Api.Modules.Finance.Export;
using ProjectApp.Api.Modules.Finance.Clients;
using ProjectApp.Api.Data;
using ProjectApp.Api.Modules.Finance.Alerts;

namespace ProjectApp.Api.Modules.Finance;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")] // Only Admin sees finance
public class FinanceController : ControllerBase
{
    private readonly FinanceService _svc;
    private readonly FinanceCashFlowCalculator _cash;
    private readonly LiquidityService _liq;
    private readonly FinanceForecastService _forecast;
    private readonly ProductAnalysisService _analysis;
    private readonly FinanceTrendCalculator _trends;
    private readonly TaxCalculatorService _tax;
    private readonly FinanceExportService _export;
    private readonly ClientFinanceReportBuilder _clients;
    private readonly FinanceAlertService _alerts;
    private readonly AppDbContext _db;

    public FinanceController(FinanceService svc, FinanceCashFlowCalculator cash, LiquidityService liq, FinanceForecastService forecast, ProductAnalysisService analysis, FinanceTrendCalculator trends, TaxCalculatorService tax, FinanceExportService export, ClientFinanceReportBuilder clients, FinanceAlertService alerts, AppDbContext db)
    {
        _svc = svc;
        _cash = cash;
        _liq = liq;
        _forecast = forecast;
        _analysis = analysis;
        _trends = trends;
        _tax = tax;
        _export = export;
        _clients = clients;
        _alerts = alerts;
        _db = db;
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(FinanceSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? bucketBy, [FromQuery] string? groupBy, CancellationToken ct)
    {
        var dto = await _svc.GetSummaryAsync(from, to, bucketBy, groupBy, ct);
        return Ok(dto);
    }

    [HttpGet("kpi")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKpi([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var kpi = await _svc.GetKpiAsync(from, to, ct);
        return Ok(kpi);
    }

    [HttpPost("snapshot")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateSnapshot([FromQuery] DateTime? dateUtc, CancellationToken ct)
    {
        var snap = await _svc.CreateAndPersistSnapshotAsync(dateUtc, ct);
        return Ok(new { ok = true, date = snap.Date, revenue = snap.Revenue, cogs = snap.Cogs, gross = snap.GrossProfit, net = snap.NetProfit });
    }

    [HttpGet("cashflow")]
    [ProducesResponseType(typeof(CashFlowDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCashFlow([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var f = from ?? DateTime.UtcNow.Date.AddDays(-30);
        var t = to ?? DateTime.UtcNow.Date.AddDays(1);
        if (f.Kind != DateTimeKind.Utc) f = DateTime.SpecifyKind(f, DateTimeKind.Utc);
        if (t.Kind != DateTimeKind.Utc) t = DateTime.SpecifyKind(t, DateTimeKind.Utc);
        var dto = await _cash.ComputeAsync(f, t, ct);
        return Ok(dto);
    }

    [HttpGet("liquidity")]
    [ProducesResponseType(typeof(LiquidityRatiosDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLiquidity(CancellationToken ct)
    {
        var dto = await _liq.ComputeAsync(ct);
        return Ok(dto);
    }

    [HttpGet("forecast")]
    [ProducesResponseType(typeof(ForecastDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetForecast([FromQuery] int days = 30, CancellationToken ct = default)
    {
        var dto = await _forecast.BuildForecastAsync(days, ct);
        return Ok(dto);
    }

    [HttpGet("abc")]
    [ProducesResponseType(typeof(AbcResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAbc([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var f = from ?? DateTime.UtcNow.Date.AddMonths(-1);
        var t = to ?? DateTime.UtcNow.Date.AddDays(1);
        if (f.Kind != DateTimeKind.Utc) f = DateTime.SpecifyKind(f, DateTimeKind.Utc);
        if (t.Kind != DateTimeKind.Utc) t = DateTime.SpecifyKind(t, DateTimeKind.Utc);
        var dto = await _analysis.GetAbcAsync(f, t, (_alerts.Settings.AbcThresholds.A, _alerts.Settings.AbcThresholds.B), ct);
        return Ok(dto);
    }

    [HttpGet("xyz")]
    [ProducesResponseType(typeof(XyzResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetXyz([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string bucket = "month", CancellationToken ct = default)
    {
        bucket = string.Equals(bucket, "week", StringComparison.OrdinalIgnoreCase) ? "week" : "month";
        var f = from ?? DateTime.UtcNow.Date.AddMonths(-6);
        var t = to ?? DateTime.UtcNow.Date.AddDays(1);
        if (f.Kind != DateTimeKind.Utc) f = DateTime.SpecifyKind(f, DateTimeKind.Utc);
        if (t.Kind != DateTimeKind.Utc) t = DateTime.SpecifyKind(t, DateTimeKind.Utc);
        var dto = await _analysis.GetXyzAsync(f, t, bucket, (_alerts.Settings.XyzThresholds.X, _alerts.Settings.XyzThresholds.Y), ct);
        return Ok(dto);
    }

    [HttpGet("trends")]
    [ProducesResponseType(typeof(TrendDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrends([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string metric = "revenue", [FromQuery] string interval = "month", CancellationToken ct = default)
    {
        var f = from ?? DateTime.UtcNow.Date.AddMonths(-12);
        var t = to ?? DateTime.UtcNow.Date.AddDays(1);
        if (f.Kind != DateTimeKind.Utc) f = DateTime.SpecifyKind(f, DateTimeKind.Utc);
        if (t.Kind != DateTimeKind.Utc) t = DateTime.SpecifyKind(t, DateTimeKind.Utc);
        var dto = await _trends.ComputeAsync(f, t, metric, interval, ct);
        return Ok(dto);
    }

    [HttpGet("taxes/breakdown")]
    [ProducesResponseType(typeof(TaxesBreakdownDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTaxesBreakdown([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var f = from ?? DateTime.UtcNow.Date.AddMonths(-1);
        var t = to ?? DateTime.UtcNow.Date.AddDays(1);
        if (f.Kind != DateTimeKind.Utc) f = DateTime.SpecifyKind(f, DateTimeKind.Utc);
        if (t.Kind != DateTimeKind.Utc) t = DateTime.SpecifyKind(t, DateTimeKind.Utc);
        var dto = await _tax.ComputeBreakdownAsync(f, t, ct);
        return Ok(dto);
    }

    [HttpGet("expenses")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenses([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? category, CancellationToken ct)
    {
        var f = from ?? DateTime.UtcNow.Date.AddMonths(-1);
        var t = to ?? DateTime.UtcNow.Date.AddDays(1);
        if (f.Kind != DateTimeKind.Utc) f = DateTime.SpecifyKind(f, DateTimeKind.Utc);
        if (t.Kind != DateTimeKind.Utc) t = DateTime.SpecifyKind(t, DateTimeKind.Utc);
        var q = _db.Expenses.AsQueryable().AsNoTracking().Where(e => e.Date >= f && e.Date < t);
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(e => e.Category == category);
        var total = await q.SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var byCat = await _db.Expenses.AsNoTracking().Where(e => e.Date >= f && e.Date < t)
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key, Amount = g.Sum(x => x.Amount) }).ToListAsync(ct);
        return Ok(new { total, byCategory = byCat });
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string format = "excel", [FromQuery] string? groupBy = null, CancellationToken ct = default)
    {
        var (content, name, contentType) = await _export.ExportSummaryAsync(from, to, format, groupBy, ct);
        return File(content, contentType, name);
    }

    [HttpGet("clients")]
    [ProducesResponseType(typeof(ClientFinanceDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClients([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var f = from ?? DateTime.UtcNow.Date.AddMonths(-1);
        var t = to ?? DateTime.UtcNow.Date.AddDays(1);
        if (f.Kind != DateTimeKind.Utc) f = DateTime.SpecifyKind(f, DateTimeKind.Utc);
        if (t.Kind != DateTimeKind.Utc) t = DateTime.SpecifyKind(t, DateTimeKind.Utc);
        var dto = await _clients.BuildAsync(f, t, ct);
        return Ok(dto);
    }

    [HttpGet("alerts/preview")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAlerts([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var res = await _alerts.EvaluateAsync(from, to, ct);
        return Ok(res);
    }
}
