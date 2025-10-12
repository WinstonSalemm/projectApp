using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Modules.Finance.Dtos;

namespace ProjectApp.Api.Modules.Finance;

public class FinanceReportBuilder(AppDbContext db)
{
    private readonly AppDbContext _db = db;

    public async Task<List<GroupPoint>> GroupByCategoryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var rows = await (from s in _db.Sales.AsNoTracking()
                          where s.CreatedAt >= fromUtc && s.CreatedAt < toUtc
                          join i in _db.SaleItems.AsNoTracking() on s.Id equals i.SaleId
                          join p in _db.Products.AsNoTracking() on i.ProductId equals p.Id
                          select new { p.Category, i.UnitPrice, i.Qty, i.Cost })
                         .ToListAsync(ct);
        return rows
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Category) ? "Без категории" : x.Category)
            .Select(g => new GroupPoint(
                Key: g.Key,
                Revenue: g.Sum(x => x.UnitPrice * x.Qty),
                Cogs: g.Sum(x => x.Cost * x.Qty),
                Gross: g.Sum(x => x.UnitPrice * x.Qty) - g.Sum(x => x.Cost * x.Qty)))
            .OrderByDescending(x => x.Gross)
            .ToList();
    }

    public async Task<List<GroupPoint>> GroupByManagerAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var rows = await (from s in _db.Sales.AsNoTracking()
                          where s.CreatedAt >= fromUtc && s.CreatedAt < toUtc
                          join i in _db.SaleItems.AsNoTracking() on s.Id equals i.SaleId
                          select new { s.CreatedBy, i.UnitPrice, i.Qty, i.Cost })
                         .ToListAsync(ct);
        return rows
            .GroupBy(x => string.IsNullOrWhiteSpace(x.CreatedBy) ? "unknown" : x.CreatedBy)
            .Select(g => new GroupPoint(
                Key: g.Key,
                Revenue: g.Sum(x => x.UnitPrice * x.Qty),
                Cogs: g.Sum(x => x.Cost * x.Qty),
                Gross: g.Sum(x => x.UnitPrice * x.Qty) - g.Sum(x => x.Cost * x.Qty)))
            .OrderByDescending(x => x.Gross)
            .ToList();
    }
}
