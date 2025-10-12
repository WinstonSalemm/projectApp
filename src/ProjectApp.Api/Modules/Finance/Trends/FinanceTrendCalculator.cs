using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Modules.Finance.Trends;

namespace ProjectApp.Api.Modules.Finance.Trends;

public class FinanceTrendCalculator(AppDbContext db)
{
    private readonly AppDbContext _db = db;

    public async Task<TrendDto> ComputeAsync(DateTime fromUtc, DateTime toUtc, string metric, string interval, CancellationToken ct)
    {
        metric = string.IsNullOrWhiteSpace(metric) ? "revenue" : metric.ToLowerInvariant();
        interval = string.IsNullOrWhiteSpace(interval) ? "month" : interval.ToLowerInvariant();

        DateTime Trunc(DateTime dt)
        {
            return interval switch
            {
                "quarter" => new DateTime(dt.Year, ((dt.Month - 1) / 3) * 3 + 1, 1, 0, 0, 0, DateTimeKind.Utc),
                "year" => new DateTime(dt.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                _ => new DateTime(dt.Year, dt.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            };
        }

        var sales = await (from s in _db.Sales.AsNoTracking()
                           where s.CreatedAt >= fromUtc && s.CreatedAt < toUtc
                           join i in _db.SaleItems.AsNoTracking() on s.Id equals i.SaleId
                           select new { s.CreatedAt, Rev = i.UnitPrice * i.Qty, Cogs = i.Cost * i.Qty })
                           .ToListAsync(ct);

        var groups = sales.GroupBy(x => Trunc(x.CreatedAt)).OrderBy(g => g.Key).Select(g => new
        {
            Period = DateOnly.FromDateTime(g.Key),
            Revenue = g.Sum(x => x.Rev),
            Gross = g.Sum(x => x.Rev) - g.Sum(x => x.Cogs),
            Net = g.Sum(x => x.Rev) - g.Sum(x => x.Cogs) // net approximated; detailed net needs expenses/taxes per period; simplify here
        }).ToList();

        var series = groups.Select(g => new TrendPoint { Period = g.Period, Value = metric == "gross" ? g.Gross : metric == "net" ? g.Net : g.Revenue }).ToList();

        decimal? mom = null, yoy = null;
        if (series.Count >= 2)
        {
            var last = series[^1].Value; var prev = series[^2].Value; if (prev != 0) mom = decimal.Round((last - prev) / prev * 100m, 2);
        }
        // YoY: find same period last year
        var lastPeriod = series.Count > 0 ? groups[^1].Period : (DateOnly?)null;
        if (lastPeriod.HasValue)
        {
            var prevYear = lastPeriod.Value.AddYears(-1);
            var match = series.FirstOrDefault(p => p.Period == prevYear);
            if (match is not null && match.Value != 0)
            {
                yoy = decimal.Round((series[^1].Value - match.Value) / match.Value * 100m, 2);
            }
        }

        return new TrendDto { Metric = metric, Interval = interval, Series = series, MoMPercent = mom, YoYPercent = yoy };
    }
}
