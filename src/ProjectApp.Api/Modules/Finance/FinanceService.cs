using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectApp.Api.Data;
using ProjectApp.Api.Modules.Finance.Dtos;
using ProjectApp.Api.Modules.Finance.Models;

namespace ProjectApp.Api.Modules.Finance;

public class FinanceService
{
    private readonly IFinanceRepository _repo;
    private readonly FinanceMetricsCalculator _calc;
    private readonly AppDbContext _db;
    private readonly FinanceSettings _settings;
    private readonly FinanceReportBuilder _report;

    public FinanceService(IFinanceRepository repo, FinanceMetricsCalculator calc, AppDbContext db, IOptions<FinanceSettings> settings, FinanceReportBuilder report)
    {
        _repo = repo;
        _calc = calc;
        _db = db;
        _settings = settings.Value;
        _report = report;
    }

    private static (DateTime fromUtc, DateTime toUtc) NormalizeRangeUtc(DateTime? from, DateTime? to)
    {
        var f = from ?? DateTime.UtcNow.Date;
        var t = to ?? DateTime.UtcNow.Date.AddDays(1);
        if (f.Kind != DateTimeKind.Utc) f = DateTime.SpecifyKind(f, DateTimeKind.Utc);
        if (t.Kind != DateTimeKind.Utc) t = DateTime.SpecifyKind(t, DateTimeKind.Utc);
        return (f, t);
    }

    public async Task<FinanceSummaryDto> GetSummaryAsync(DateTime? from, DateTime? to, string? bucketBy, string? groupBy, CancellationToken ct)
    {
        var (fromUtc, toUtc) = NormalizeRangeUtc(from, to);

        var (revenue, cogs, salesCount, uniqueClients) = await _repo.GetSalesBlockAsync(fromUtc, toUtc, ct);
        var expenses = await _repo.GetExpensesAsync(fromUtc, toUtc, ct);
        var taxesPaid = await _repo.GetTaxesPaidAsync(fromUtc, toUtc, ct);
        var avgInv = await _repo.GetAverageInventoryQtyAsync(fromUtc, toUtc, ct);
        var (gross, marginPct, net) = _calc.ComputeProfit(revenue, cogs, expenses, taxesPaid);

        object? series = null;
        if (!string.IsNullOrWhiteSpace(bucketBy))
        {
            var normalized = bucketBy!.ToLowerInvariant();
            if (normalized != "day" && normalized != "week" && normalized != "month") normalized = "day";
            var buckets = await _repo.GetSalesBucketsAsync(fromUtc, toUtc, normalized, ct);
            series = buckets.Select(b => new { date = b.bucket, revenue = b.revenue, cogs = b.cogs, gross = b.revenue - b.cogs }).ToList();
        }

        IEnumerable<GroupPoint>? groups = null;
        if (!string.IsNullOrWhiteSpace(groupBy))
        {
            var g = groupBy!.ToLowerInvariant();
            if (g == "category") groups = await _report.GroupByCategoryAsync(fromUtc, toUtc, ct);
            else if (g == "manager") groups = await _report.GroupByManagerAsync(fromUtc, toUtc, ct);
        }

        return new FinanceSummaryDto(
            Revenue: revenue,
            Cogs: cogs,
            GrossProfit: gross,
            NetProfit: net,
            MarginPercent: marginPct,
            Expenses: expenses,
            TaxesPaid: taxesPaid,
            SalesCount: salesCount,
            UniqueClients: uniqueClients,
            AverageInventory: avgInv,
            Series: series,
            Groups: groups);
    }

    public async Task<FinanceKpi> GetKpiAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (fromUtc, toUtc) = NormalizeRangeUtc(from, to);
        var (revenue, cogs, salesCount, uniqueClients) = await _repo.GetSalesBlockAsync(fromUtc, toUtc, ct);
        var expenses = await _repo.GetExpensesAsync(fromUtc, toUtc, ct);
        var taxesPaid = await _repo.GetTaxesPaidAsync(fromUtc, toUtc, ct);
        var avgInv = await _repo.GetAverageInventoryQtyAsync(fromUtc, toUtc, ct);
        var (gross, _, net) = _calc.ComputeProfit(revenue, cogs, expenses, taxesPaid);

        var totalDebts = await _db.Debts.AsNoTracking().SumAsync(d => (decimal?)d.Amount, ct) ?? 0m;
        var paidDebts = await _db.DebtPayments.AsNoTracking().SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var outstandingDebts = totalDebts - paidDebts;

        return _calc.ComputeKpi(
            netProfit: net,
            revenue: revenue,
            cogs: cogs,
            salesCount: salesCount,
            uniqueClients: uniqueClients,
            averageInventoryQty: avgInv,
            totalDebts: outstandingDebts,
            totalInvestments: _settings.TotalInvestments,
            totalAssets: _settings.TotalAssets,
            equity: _settings.Equity);
    }

    public async Task<FinanceSnapshot> BuildSnapshotAsync(DateTime dayUtc, CancellationToken ct)
    {
        var from = new DateTime(dayUtc.Year, dayUtc.Month, dayUtc.Day, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(1);
        var (revenue, cogs, salesCount, uniqueClients) = await _repo.GetSalesBlockAsync(from, to, ct);
        var expenses = await _repo.GetExpensesAsync(from, to, ct);
        var taxesPaid = await _repo.GetTaxesPaidAsync(from, to, ct);
        var avgInv = await _repo.GetAverageInventoryQtyAsync(from, to, ct);
        var (gross, _, net) = _calc.ComputeProfit(revenue, cogs, expenses, taxesPaid);
        return new FinanceSnapshot
        {
            Date = from,
            Revenue = revenue,
            Cogs = cogs,
            GrossProfit = gross,
            Expenses = expenses,
            TaxesPaid = taxesPaid,
            NetProfit = net,
            SalesCount = salesCount,
            UniqueClients = uniqueClients,
            AverageInventory = avgInv
        };
    }

    public async Task<FinanceSnapshot> CreateAndPersistSnapshotAsync(DateTime? dayUtc, CancellationToken ct)
    {
        var d = (dayUtc ?? DateTime.UtcNow.Date);
        if (d.Kind != DateTimeKind.Utc) d = DateTime.SpecifyKind(d, DateTimeKind.Utc);
        var snap = await BuildSnapshotAsync(d, ct);
        var existing = await _db.FinanceSnapshots.FirstOrDefaultAsync(x => x.Date == snap.Date, ct);
        if (existing is null) _db.FinanceSnapshots.Add(snap);
        else
        {
            existing.Revenue = snap.Revenue;
            existing.Cogs = snap.Cogs;
            existing.GrossProfit = snap.GrossProfit;
            existing.Expenses = snap.Expenses;
            existing.TaxesPaid = snap.TaxesPaid;
            existing.NetProfit = snap.NetProfit;
            existing.SalesCount = snap.SalesCount;
            existing.UniqueClients = snap.UniqueClients;
            existing.AverageInventory = snap.AverageInventory;
        }
        await _db.SaveChangesAsync(ct);
        return existing ?? snap;
    }
}
