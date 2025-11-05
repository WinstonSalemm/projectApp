using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
using ProjectApp.Api.Integrations.Telegram;
using Microsoft.Extensions.Options;
using System.Text;

namespace ProjectApp.Api.Modules.Finance;

[ApiController]
[Route("api/[controller]")]
//[Authorize(Policy = "AdminOnly")] // Временно отключено для теста
[AllowAnonymous]
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
    private readonly ITelegramService _tg;
    private readonly TelegramSettings _tgSettings;

    public FinanceController(FinanceService svc, FinanceCashFlowCalculator cash, LiquidityService liq, FinanceForecastService forecast, ProductAnalysisService analysis, FinanceTrendCalculator trends, TaxCalculatorService tax, FinanceExportService export, ClientFinanceReportBuilder clients, FinanceAlertService alerts, AppDbContext db, ITelegramService tg, IOptions<TelegramSettings> tgOptions)
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
        _tg = tg;
        _tgSettings = tgOptions.Value;
    }

    [HttpGet("total-revenue")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTotalRevenue(CancellationToken ct)
    {
        try
        {
            var total = await _db.Sales.AsNoTracking().SumAsync(s => (decimal?)s.Total, ct) ?? 0m;
            return Ok(new { revenue = total });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
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
        try
        {
            // Временно упрощенная версия - прямой запрос к БД
            var dateFrom = from ?? DateTime.UtcNow.AddDays(-30);
            var dateTo = to ?? DateTime.UtcNow;
            
            var sales = await _db.Sales
                .AsNoTracking()
                .Where(s => s.CreatedAt >= dateFrom && s.CreatedAt < dateTo)
                .Include(s => s.Items)
                .ToListAsync(ct);
            
            var revenue = sales.Sum(s => s.Total);
            var cogs = sales.SelectMany(s => s.Items).Sum(i => i.Qty * i.Cost);
            var grossProfit = revenue - cogs;
            var salesCount = sales.Count;
            var ndImCashFlow = await _db.CashTransactions
                .AsNoTracking()
                .Where(t => t.CreatedAt >= dateFrom && t.CreatedAt < dateTo && t.Category == "NDIM-ADJUST")
                .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;
            
            return Ok(new
            {
                revenue,
                cogs,
                grossProfit,
                salesCount,
                marginPercent = revenue > 0 ? (grossProfit / revenue) * 100 : 0,
                ndImCashFlow
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
        }
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

    // GET /api/finance/ndim/export?from=...&to=...&sendToTelegram=true
    [HttpGet("ndim/export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportNdImAdjust([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] bool sendToTelegram = false, CancellationToken ct = default)
    {
        var f = from ?? DateTime.UtcNow.Date.AddMonths(-1);
        var t = to ?? DateTime.UtcNow.Date.AddDays(1);
        if (f.Kind != DateTimeKind.Utc) f = DateTime.SpecifyKind(f, DateTimeKind.Utc);
        if (t.Kind != DateTimeKind.Utc) t = DateTime.SpecifyKind(t, DateTimeKind.Utc);

        // Берём транзакции CASH FLOW по ND→IM
        var tx = await _db.CashTransactions
            .AsNoTracking()
            .Where(x => x.Category == "NDIM-ADJUST" && x.CreatedAt >= f && x.CreatedAt < t)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

        // Предзагрузим нужные SaleItems для восстановления qty и текущей цены (новой)
        var itemIds = new List<int>();
        foreach (var t0 in tx)
        {
            // Ожидаем формат Description: "ND→IM reprice Sale#{saleId} Item#{itemId}"
            var desc = t0.Description ?? string.Empty;
            var idx = desc.IndexOf("Item#", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var tail = desc.Substring(idx + 5);
                if (int.TryParse(new string(tail.TakeWhile(char.IsDigit).ToArray()), out var parsedItemId))
                    itemIds.Add(parsedItemId);
            }
        }

        var saleItems = await _db.SaleItems.AsNoTracking()
            .Where(si => itemIds.Contains(si.Id))
            .ToDictionaryAsync(si => si.Id, ct);

        // Сборка CSV (Excel-friendly)
        var sb = new StringBuilder();
        sb.AppendLine("Date,SaleId,ItemId,SKU,Product,Qty,OldPrice,NewPrice,DeltaPerUnit,Amount");
        foreach (var t0 in tx)
        {
            int saleId = t0.LinkedSaleId ?? 0;
            int itemId = 0;
            var desc = t0.Description ?? string.Empty;
            var idx = desc.IndexOf("Item#", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var tail = desc.Substring(idx + 5);
                int.TryParse(new string(tail.TakeWhile(char.IsDigit).ToArray()), out itemId);
            }

            saleItems.TryGetValue(itemId, out var si);
            var qty = si?.Qty ?? 0m;
            var newPrice = si?.UnitPrice ?? 0m;
            var deltaPerUnit = qty != 0 ? decimal.Round((t0.Amount / (qty == 0 ? 1 : qty)), 2, MidpointRounding.AwayFromZero) : 0m;
            var oldPrice = newPrice + deltaPerUnit;
            var sku = si?.Sku ?? string.Empty;
            var name = si?.ProductName ?? string.Empty;

            // CSV-escape name
            string Esc(string s) => string.IsNullOrEmpty(s) ? string.Empty : (s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s);

            sb.AppendLine(string.Join(',',
                t0.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                saleId,
                itemId,
                Esc(sku),
                Esc(name),
                qty.ToString(System.Globalization.CultureInfo.InvariantCulture),
                oldPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),
                newPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),
                deltaPerUnit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                t0.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            ));
        }

        var csv = sb.ToString();
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        var fileName = $"ndim-adjust-{f:yyyyMMdd}-{t.AddDays(-1):yyyyMMdd}.csv";

        if (sendToTelegram)
        {
            var ids = _tgSettings.ParseAllowedChatIds();
            foreach (var chatId in ids)
            {
                try
                {
                    await using var ms = new MemoryStream(bytes, writable: false);
                    ms.Position = 0;
                    await _tg.SendDocumentAsync(chatId, ms, fileName, caption: $"ND→IM отчёт {f:yyyy-MM-dd}..{t:yyyy-MM-dd}", ct);
                }
                catch { }
            }
        }

        return File(bytes, "text/csv", fileName);
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
