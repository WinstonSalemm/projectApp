using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Modules.Finance.Models;

namespace ProjectApp.Api.Modules.Finance;

public interface IFinanceRepository
{
    Task<(decimal revenue, decimal cogs, int salesCount, int uniqueClients)> GetSalesBlockAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct);
    Task<decimal> GetExpensesAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct);
    Task<decimal> GetTaxesPaidAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct);
    Task<decimal> GetAverageInventoryQtyAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct);
    Task<List<(DateTime bucket, decimal revenue, decimal cogs)>> GetSalesBucketsAsync(DateTime fromUtc, DateTime toUtc, string bucketBy, CancellationToken ct);
}

public class FinanceRepository(AppDbContext db) : IFinanceRepository
{
    private readonly AppDbContext _db = db;

    public async Task<(decimal revenue, decimal cogs, int salesCount, int uniqueClients)> GetSalesBlockAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var q = from s in _db.Sales.AsNoTracking()
                where s.CreatedAt >= fromUtc && s.CreatedAt < toUtc
                join i in _db.SaleItems.AsNoTracking() on s.Id equals i.SaleId
                select new { s.ClientId, i.UnitPrice, i.Qty, i.Cost };

        var list = await q.ToListAsync(ct);
        var revenue = list.Sum(x => x.UnitPrice * x.Qty);
        var cogs = list.Sum(x => x.Cost * x.Qty);

        var salesCount = await _db.Sales.AsNoTracking().Where(s => s.CreatedAt >= fromUtc && s.CreatedAt < toUtc).CountAsync(ct);
        var uniqueClients = await _db.Sales.AsNoTracking().Where(s => s.CreatedAt >= fromUtc && s.CreatedAt < toUtc && s.ClientId != null).Select(s => s.ClientId!.Value).Distinct().CountAsync(ct);
        return (revenue, cogs, salesCount, uniqueClients);
    }

    public async Task<decimal> GetExpensesAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        return await _db.Expenses.AsNoTracking().Where(e => e.Date >= fromUtc && e.Date < toUtc).SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
    }

    public async Task<decimal> GetTaxesPaidAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        return await _db.TaxPayments.AsNoTracking().Where(t => t.PaidAt >= fromUtc && t.PaidAt < toUtc).SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;
    }

    public async Task<decimal> GetAverageInventoryQtyAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var snaps = await _db.StockSnapshots.AsNoTracking()
            .Where(s => s.CreatedAt >= fromUtc && s.CreatedAt < toUtc)
            .GroupBy(s => s.CreatedAt.Date)
            .Select(g => new { Day = g.Key, Qty = g.Sum(x => x.TotalQty) })
            .ToListAsync(ct);
        if (snaps.Count == 0) return 0m;
        return snaps.Average(x => x.Qty);
    }

    public async Task<List<(DateTime bucket, decimal revenue, decimal cogs)>> GetSalesBucketsAsync(DateTime fromUtc, DateTime toUtc, string bucketBy, CancellationToken ct)
    {
        // bucketBy: day|week|month
        DateTime Trunc(DateTime dt)
        {
            if (bucketBy == "week")
            {
                var d = dt.Date;
                int diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7;
                return new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(-diff);
            }
            if (bucketBy == "month")
            {
                return new DateTime(dt.Year, dt.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            }
            return new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Utc);
        }

        var items = await (from s in _db.Sales.AsNoTracking()
                           where s.CreatedAt >= fromUtc && s.CreatedAt < toUtc
                           join i in _db.SaleItems.AsNoTracking() on s.Id equals i.SaleId
                           select new { s.CreatedAt, i.UnitPrice, i.Qty, i.Cost })
                           .ToListAsync(ct);

        var buckets = items
            .GroupBy(x => Trunc(x.CreatedAt))
            .Select(g => (
                bucket: g.Key,
                revenue: g.Sum(x => x.UnitPrice * x.Qty),
                cogs: g.Sum(x => x.Cost * x.Qty)
            ))
            .OrderBy(x => x.bucket)
            .ToList();
        return buckets;
    }
}
