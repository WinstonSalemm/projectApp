using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;

namespace ProjectApp.Api.Modules.Finance.Analysis;

public class ProductAnalysisService(AppDbContext db)
{
    private readonly AppDbContext _db = db;

    public async Task<AbcResultDto> GetAbcAsync(DateTime fromUtc, DateTime toUtc, (double A, double B) thresholds, CancellationToken ct)
    {
        var rows = await (from s in _db.Sales.AsNoTracking()
                          where s.CreatedAt >= fromUtc && s.CreatedAt < toUtc
                          join i in _db.SaleItems.AsNoTracking() on s.Id equals i.SaleId
                          join p in _db.Products.AsNoTracking() on i.ProductId equals p.Id
                          group new { i, p } by new { i.ProductId, p.Name } into g
                          select new { g.Key.ProductId, g.Key.Name, Revenue = g.Sum(x => x.i.UnitPrice * x.i.Qty), Cogs = g.Sum(x => x.i.Cost * x.i.Qty) })
                          .OrderByDescending(x => x.Revenue)
                          .ToListAsync(ct);
        var total = rows.Sum(x => x.Revenue);
        decimal acc = 0m;
        var items = new List<AbcItem>();
        foreach (var r in rows)
        {
            acc += r.Revenue;
            var share = total == 0 ? 0 : (r.Revenue / total);
            string cls = share <= (decimal)thresholds.A && acc / (total == 0 ? 1 : total) <= (decimal)thresholds.A ? "A"
                        : acc / (total == 0 ? 1 : total) <= (decimal)thresholds.B ? "B" : "C";
            items.Add(new AbcItem { ProductId = r.ProductId, Name = r.Name, Revenue = r.Revenue, Share = decimal.Round(share * 100m, 2), Class = cls });
        }
        return new AbcResultDto { TotalRevenue = total, Items = items };
    }

    public async Task<XyzResultDto> GetXyzAsync(DateTime fromUtc, DateTime toUtc, string bucket, (double X, double Y) thresholds, CancellationToken ct)
    {
        // bucket by week or month
        DateTime Trunc(DateTime dt)
        {
            if (bucket == "month") return new DateTime(dt.Year, dt.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var d = dt.Date; int diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7; return new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(-diff);
        }
        var rows = await (from s in _db.Sales.AsNoTracking()
                          where s.CreatedAt >= fromUtc && s.CreatedAt < toUtc
                          join i in _db.SaleItems.AsNoTracking() on s.Id equals i.SaleId
                          join p in _db.Products.AsNoTracking() on i.ProductId equals p.Id
                          select new { s.CreatedAt, i.ProductId, p.Name, Qty = i.Qty })
                          .ToListAsync(ct);
        var groups = rows.GroupBy(r => new { r.ProductId, r.Name }).Select(g => new
        {
            g.Key.ProductId,
            g.Key.Name,
            Series = g.GroupBy(x => Trunc(x.CreatedAt)).Select(gg => new { Date = gg.Key, Qty = gg.Sum(x => x.Qty) }).OrderBy(x => x.Date).ToList()
        }).ToList();

        string Classify(decimal mean, decimal sd)
        {
            if (mean == 0) return "Z";
            var cv = sd / (mean == 0 ? 1 : mean);
            if ((double)cv <= thresholds.X) return "X";
            if ((double)cv <= thresholds.Y) return "Y";
            return "Z";
        }

        var items = new List<XyzItem>();
        foreach (var g in groups)
        {
            var vals = g.Series.Select(x => x.Qty).ToList();
            if (vals.Count == 0) { items.Add(new XyzItem { ProductId = g.ProductId, Name = g.Name, MeanQty = 0, Cv = 0, Class = "Z" }); continue; }
            var mean = vals.Average();
            var variance = vals.Count <= 1 ? 0m : vals.Select(v => (v - mean) * (v - mean)).Sum() / (vals.Count - 1);
            var sd = (decimal)Math.Sqrt((double)variance);
            var cv = mean == 0 ? 0 : (sd / mean);
            items.Add(new XyzItem { ProductId = g.ProductId, Name = g.Name, MeanQty = decimal.Round(mean, 3), Cv = decimal.Round(cv, 3), Class = Classify(mean, sd) });
        }
        return new XyzResultDto { Items = items };
    }
}
