using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using System.Globalization;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController(AppDbContext db) : ControllerBase
{
    public record SalesGroupDto(string Key, decimal TotalAmount, decimal TotalQty, int SalesCount, string? TopSeller, decimal TopSellerAmount);

    [HttpGet("sales")]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> Sales([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? groupBy, [FromQuery] string? preset, CancellationToken ct)
    {
        var (f, t) = ResolveRange(from, to, preset);
        groupBy = string.IsNullOrWhiteSpace(groupBy) ? "day" : groupBy!.ToLowerInvariant();

        var rows = await db.Sales
            .AsNoTracking()
            .Where(s => s.CreatedAt >= f && s.CreatedAt < t)
            .Select(s => new
            {
                s.Id,
                s.Total,
                s.CreatedAt,
                s.CreatedBy,
                Qty = s.Items.Sum(i => i.Qty)
            })
            .ToListAsync(ct);

        // Summary
        var summaryTotal = rows.Sum(r => r.Total);
        var summaryQty = rows.Sum(r => r.Qty);
        var summaryCount = rows.Count;
        var topByAmount = rows
            .GroupBy(r => r.CreatedBy ?? "unknown")
            .Select(g => new { Seller = g.Key, Amount = g.Sum(x => x.Total) })
            .OrderByDescending(x => x.Amount)
            .FirstOrDefault();

        // Grouping
        IEnumerable<IGrouping<string, dynamic>> groups = groupBy switch
        {
            "week" => rows.GroupBy(r => WeekKey(r.CreatedAt)),
            "month" => rows.GroupBy(r => r.CreatedAt.ToString("yyyy-MM")),
            _ => rows.GroupBy(r => r.CreatedAt.ToString("yyyy-MM-dd"))
        };

        var resultGroups = groups
            .OrderBy(g => g.Key)
            .Select(g => new SalesGroupDto(
                Key: g.Key,
                TotalAmount: g.Sum(x => (decimal)x.Total),
                TotalQty: g.Sum(x => (decimal)x.Qty),
                SalesCount: g.Count(),
                TopSeller: g.GroupBy(x => (string?)(x.CreatedBy ?? "unknown"))
                            .Select(gg => new { Seller = gg.Key, Amount = gg.Sum(x => (decimal)x.Total) })
                            .OrderByDescending(x => x.Amount)
                            .FirstOrDefault()?.Seller,
                TopSellerAmount: g.GroupBy(x => (string?)(x.CreatedBy ?? "unknown"))
                                   .Select(gg => new { Seller = gg.Key, Amount = gg.Sum(x => (decimal)x.Total) })
                                   .OrderByDescending(x => x.Amount)
                                   .FirstOrDefault()?.Amount ?? 0m
            ));

        return Ok(new
        {
            from = f,
            to = t,
            groupBy,
            groups = resultGroups,
            summary = new
            {
                totalAmount = summaryTotal,
                totalQty = summaryQty,
                salesCount = summaryCount,
                topSeller = topByAmount?.Seller,
                topSellerAmount = topByAmount?.Amount ?? 0m
            }
        });
    }

    private static (DateTime From, DateTime To) ResolveRange(DateTime? from, DateTime? to, string? preset)
    {
        if (!string.IsNullOrWhiteSpace(preset))
        {
            var now = DateTime.UtcNow;
            switch (preset.ToLowerInvariant())
            {
                case "today":
                    var start = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
                    return (start, start.AddDays(1));
                case "week":
                    var monday = StartOfWeekUtc(now);
                    return (monday, monday.AddDays(7));
                case "month":
                    var first = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    return (first, first.AddMonths(1));
            }
        }
        var f = from ?? DateTime.UtcNow.AddDays(-1);
        var t = to ?? DateTime.UtcNow;
        return (f, t);
    }

    private static string WeekKey(DateTime dt)
    {
        var monday = StartOfWeekUtc(dt);
        return monday.ToString("yyyy-MM-dd");
    }

    private static DateTime StartOfWeekUtc(DateTime dt)
    {
        var d = dt.Date;
        int diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(-diff);
    }
}
