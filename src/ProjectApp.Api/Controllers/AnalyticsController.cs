using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using System.Globalization;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")] // аналитика доступна только админам
public class AnalyticsController(AppDbContext db) : ControllerBase
{
    // GET /api/analytics/products?from=2025-01-01&to=2025-12-31&metric=revenue&top=10&format=json
    [HttpGet("products")]
    public async Task<IActionResult> Products([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string metric = "revenue", [FromQuery] int top = 10, [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        var dateFrom = from ?? DateTime.UtcNow.Date;
        var dateTo = to ?? DateTime.UtcNow.Date.AddDays(1);
        var q = await db.Sales
            .AsNoTracking()
            .Where(s => s.CreatedAt >= dateFrom && s.CreatedAt < dateTo)
            .SelectMany(s => s.Items.Select(i => new { s.CreatedAt, i.ProductId, i.Qty, i.UnitPrice, i.Cost }))
            .ToListAsync(ct);

        var grouped = q
            .GroupBy(x => x.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Qty = g.Sum(x => x.Qty),
                Revenue = g.Sum(x => x.Qty * x.UnitPrice),
                Margin = g.Sum(x => x.Qty * (x.UnitPrice - x.Cost))
            })
            .ToList();

        var products = await db.Products.AsNoTracking().ToDictionaryAsync(p => p.Id, ct);
        var rows = grouped
            .Select(r => new
            {
                r.ProductId,
                Sku = products.TryGetValue(r.ProductId, out var p) ? p.Sku : "",
                Name = products.TryGetValue(r.ProductId, out var p2) ? p2.Name : "",
                Qty = r.Qty,
                Revenue = decimal.Round(r.Revenue, 2),
                Margin = decimal.Round(r.Margin, 2)
            })
            .ToList();

        IEnumerable<dynamic> ordered = metric.ToLowerInvariant() switch
        {
            "qty" => rows.OrderByDescending(r => r.Qty),
            "margin" => rows.OrderByDescending(r => r.Margin),
            _ => rows.OrderByDescending(r => r.Revenue)
        };

        var result = ordered.Take(Math.Clamp(top, 1, 100)).ToList();

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = ToCsv(result, new[] { "ProductId", "Sku", "Name", "Qty", "Revenue", "Margin" });
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"products_{dateFrom:yyyyMMdd}-{dateTo:yyyyMMdd}.csv");
        }

        return Ok(new { from = dateFrom, to = dateTo, metric = metric.ToLowerInvariant(), items = result });
    }

    // GET /api/analytics/sellers?from=...&to=...&metric=revenue&top=10&format=json
    [HttpGet("sellers")]
    public async Task<IActionResult> Sellers([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string metric = "revenue", [FromQuery] int top = 10, [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        var dateFrom = from ?? DateTime.UtcNow.Date;
        var dateTo = to ?? DateTime.UtcNow.Date.AddDays(1);
        var q = await db.Sales
            .AsNoTracking()
            .Where(s => s.CreatedAt >= dateFrom && s.CreatedAt < dateTo)
            .Select(s => new
            {
                s.CreatedBy,
                Revenue = s.Items.Sum(i => i.Qty * i.UnitPrice),
                Qty = s.Items.Sum(i => i.Qty),
                Margin = s.Items.Sum(i => i.Qty * (i.UnitPrice - i.Cost))
            })
            .ToListAsync(ct);

        var grouped = q
            .GroupBy(x => x.CreatedBy ?? "unknown")
            .Select(g => new
            {
                Seller = g.Key,
                Qty = g.Sum(x => x.Qty),
                Revenue = g.Sum(x => x.Revenue),
                Margin = g.Sum(x => x.Margin)
            })
            .ToList();

        IEnumerable<dynamic> ordered = metric.ToLowerInvariant() switch
        {
            "qty" => grouped.OrderByDescending(r => r.Qty),
            "margin" => grouped.OrderByDescending(r => r.Margin),
            _ => grouped.OrderByDescending(r => r.Revenue)
        };

        var result = ordered.Take(Math.Clamp(top, 1, 100)).ToList();

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = ToCsv(result, new[] { "Seller", "Qty", "Revenue", "Margin" });
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"sellers_{dateFrom:yyyyMMdd}-{dateTo:yyyyMMdd}.csv");
        }

        return Ok(new { from = dateFrom, to = dateTo, metric = metric.ToLowerInvariant(), items = result });
    }

    private static string ToCsv<T>(IEnumerable<T> rows, string[] headers)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Join(",", headers));
        var props = typeof(T).GetProperties();
        foreach (var r in rows)
        {
            var vals = headers.Select(h =>
            {
                var pi = props.FirstOrDefault(p => string.Equals(p.Name, h, StringComparison.OrdinalIgnoreCase));
                var v = pi?.GetValue(r);
                return EscapeCsv(v);
            });
            sb.AppendLine(string.Join(",", vals));
        }
        return sb.ToString();
    }

    private static string EscapeCsv(object? v)
    {
        if (v is null) return "";
        if (v is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var s = Convert.ToString(v, CultureInfo.InvariantCulture) ?? string.Empty;
        if (s.Contains('"') || s.Contains(',') || s.Contains('\n'))
        {
            s = s.Replace("\"", "\"\"");
            return $"\"{s}\"";
        }
        return s;
    }
}
