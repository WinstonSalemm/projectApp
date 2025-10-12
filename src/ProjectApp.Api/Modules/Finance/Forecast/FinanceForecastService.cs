using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;

namespace ProjectApp.Api.Modules.Finance.Forecast;

public class FinanceForecastService(AppDbContext db)
{
    private readonly AppDbContext _db = db;

    public async Task<ForecastDto> BuildForecastAsync(int days, CancellationToken ct)
    {
        if (days <= 0) days = 30;
        days = Math.Min(days, 90);

        var snapshots = await _db.FinanceSnapshots.AsNoTracking()
            .OrderBy(s => s.Date)
            .ToListAsync(ct);
        if (snapshots.Count == 0)
        {
            return new ForecastDto { Points = Array.Empty<ForecastPoint>(), Model = "empty" };
        }

        // Build daily series
        var rev = snapshots.Select(s => new { Date = DateOnly.FromDateTime(s.Date), s.Revenue, s.NetProfit }).ToList();

        // SMA30
        decimal SmaAt(int idx, int k)
        {
            if (idx < 0) return 0;
            var start = Math.Max(0, idx - k + 1);
            var count = idx - start + 1;
            if (count <= 0) return 0;
            var sum = rev.Skip(start).Take(count).Sum(x => x.Revenue);
            return count == 0 ? 0 : sum / count;
        }

        // Linear regression y = a + b*t
        (decimal a, decimal b) LinReg()
        {
            int n = rev.Count;
            if (n < 2) return (rev.Last().Revenue, 0);
            decimal sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
            for (int i = 0; i < n; i++)
            {
                decimal x = i;
                decimal y = rev[i].Revenue;
                sumX += x; sumY += y; sumXY += x * y; sumXX += x * x;
            }
            var denom = (n * sumXX - sumX * sumX);
            if (denom == 0) return (rev.Last().Revenue, 0);
            var b = (n * sumXY - sumX * sumY) / denom;
            var a = (sumY - b * sumX) / n;
            return (a, b);
        }

        var (a, b) = LinReg();
        var hasSeason = rev.Count >= 365;
        var points = new List<ForecastPoint>();
        int lastIdx = rev.Count - 1;

        for (int d = 1; d <= days; d++)
        {
            int idx = lastIdx + d;
            var sma30 = SmaAt(lastIdx, 30);
            var lr = a + b * idx;
            var baseForecast = (sma30 + lr) / 2m;
            if (baseForecast < 0) baseForecast = 0;

            if (hasSeason)
            {
                int seasonIdx = idx - 365;
                if (seasonIdx >= 0 && seasonIdx < rev.Count)
                {
                    var seasonBase = SmaAt(seasonIdx, 30);
                    var seasonVal = rev[seasonIdx].Revenue;
                    var factor = seasonBase == 0 ? 1m : (seasonVal / seasonBase);
                    baseForecast *= factor;
                }
            }

            points.Add(new ForecastPoint
            {
                Date = DateOnly.FromDateTime(rev.Last().Date.ToDateTime(TimeOnly.MinValue).AddDays(d)),
                Revenue = decimal.Round(baseForecast, 2, MidpointRounding.AwayFromZero),
                NetProfit = null
            });
        }

        return new ForecastDto { Points = points, Model = hasSeason ? "SMA30+LR+Seasonal" : "SMA30+LR" };
    }
}
