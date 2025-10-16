using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using System.Globalization;
using System.Linq;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")] // аналитика доступна только админам
public class AnalyticsController(AppDbContext db) : ControllerBase
{
    private sealed class ManagerStatsRow
    {
        public string? ManagerUserName { get; set; }
        public string? ManagerDisplayName { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal OwnClientsRevenue { get; set; }
        public int ClientsCount { get; set; }
    }
    // GET /api/analytics/managers - статистика по менеджерам
    [HttpGet("managers")]
    [AllowAnonymous] // Временно разрешаем без авторизации
    public async Task<IActionResult> Managers(CancellationToken ct = default)
    {
        try
        {
            // Получаем всех активных менеджеров
            var users = await db.Users
                .AsNoTracking()
                .Where(u => u.IsActive && u.Role == "Manager")
                .ToListAsync(ct);

            var stats = new List<ManagerStatsRow>();

            foreach (var user in users)
            {
                // Общий оборот: все продажи где менеджер = этот пользователь
                var totalRevenue = await db.Sales
                    .AsNoTracking()
                    .Where(s => s.CreatedBy == user.UserName)
                    .SelectMany(s => s.Items)
                    .SumAsync(i => i.Qty * i.UnitPrice, ct);

                // Оборот "своим" клиентам: продажи где менеджер = пользователь И клиент принадлежит этому пользователю
                var ownClientsRevenue = await db.Sales
                    .AsNoTracking()
                    .Where(s => s.CreatedBy == user.UserName && s.ClientId != null)
                    .Where(s => db.Clients.Any(c => c.Id == s.ClientId && c.OwnerUserName == user.UserName))
                    .SelectMany(s => s.Items)
                    .SumAsync(i => i.Qty * i.UnitPrice, ct);

                // Количество приведенных клиентов
                var clientsCount = await db.Clients
                    .AsNoTracking()
                    .CountAsync(c => c.OwnerUserName == user.UserName, ct);

                stats.Add(new ManagerStatsRow
                {
                    ManagerUserName = user.UserName,
                    ManagerDisplayName = user.DisplayName,
                    TotalRevenue = totalRevenue,
                    OwnClientsRevenue = ownClientsRevenue,
                    ClientsCount = clientsCount
                });
            }

            return Ok(stats.OrderByDescending(s => s.OwnClientsRevenue));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private sealed class ProductRow
    {
        public int ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public decimal Revenue { get; set; }
        public decimal Margin { get; set; }
    }

    private sealed class SellerRow
    {
        public string Seller { get; set; } = "unknown";
        public decimal Qty { get; set; }
        public decimal Revenue { get; set; }
        public decimal Margin { get; set; }
    }
    // GET /api/analytics/products?from=2025-01-01&to=2025-12-31&metric=revenue&top=10&format=json
    [HttpGet("products")]
    public async Task<IActionResult> Products([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string metric = "revenue", [FromQuery] int top = 10, [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        var dateFrom = from ?? DateTime.UtcNow.Date;
        var dateTo = to ?? DateTime.UtcNow.Date.AddDays(1);

        List<ProductRow> rows;
        try
        {
            var q = await db.Sales
                .AsNoTracking()
                .Where(s => s.CreatedAt >= dateFrom && s.CreatedAt < dateTo)
                .SelectMany(s => s.Items.Select(i => new { i.ProductId, i.Qty, i.UnitPrice, i.Cost }))
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
            rows = grouped.Select(r => new ProductRow
            {
                ProductId = r.ProductId,
                Sku = products.TryGetValue(r.ProductId, out var p) ? p.Sku : string.Empty,
                Name = products.TryGetValue(r.ProductId, out var p2) ? p2.Name : string.Empty,
                Qty = r.Qty,
                Revenue = decimal.Round(r.Revenue, 2),
                Margin = decimal.Round(r.Margin, 2)
            }).ToList();
        }
        catch
        {
            var q2 = await db.Sales
                .AsNoTracking()
                .Where(s => s.CreatedAt >= dateFrom && s.CreatedAt < dateTo)
                .SelectMany(s => s.Items.Select(i => new { i.ProductId, i.Qty, i.UnitPrice }))
                .ToListAsync(ct);

            var grouped2 = q2
                .GroupBy(x => x.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    Qty = g.Sum(x => x.Qty),
                    Revenue = g.Sum(x => x.Qty * x.UnitPrice)
                })
                .ToList();

            var products = await db.Products.AsNoTracking().ToDictionaryAsync(p => p.Id, ct);
            rows = grouped2.Select(r => new ProductRow
            {
                ProductId = r.ProductId,
                Sku = products.TryGetValue(r.ProductId, out var p) ? p.Sku : string.Empty,
                Name = products.TryGetValue(r.ProductId, out var p2) ? p2.Name : string.Empty,
                Qty = r.Qty,
                Revenue = decimal.Round(r.Revenue, 2),
                Margin = 0m
            }).ToList();
        }

        IEnumerable<ProductRow> ordered = metric.ToLowerInvariant() switch
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
        List<SellerRow> rows;
        try
        {
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

            rows = q
                .GroupBy(x => x.CreatedBy ?? "unknown")
                .Select(g => new SellerRow
                {
                    Seller = g.Key,
                    Qty = g.Sum(x => x.Qty),
                    Revenue = g.Sum(x => x.Revenue),
                    Margin = g.Sum(x => x.Margin)
                })
                .ToList();
        }
        catch
        {
            var q = await db.Sales
                .AsNoTracking()
                .Where(s => s.CreatedAt >= dateFrom && s.CreatedAt < dateTo)
                .Select(s => new
                {
                    s.CreatedBy,
                    Revenue = s.Items.Sum(i => i.Qty * i.UnitPrice),
                    Qty = s.Items.Sum(i => i.Qty)
                })
                .ToListAsync(ct);

            rows = q
                .GroupBy(x => x.CreatedBy ?? "unknown")
                .Select(g => new SellerRow
                {
                    Seller = g.Key,
                    Qty = g.Sum(x => x.Qty),
                    Revenue = g.Sum(x => x.Revenue),
                    Margin = 0m
                })
                .ToList();
        }

        IEnumerable<SellerRow> ordered = metric.ToLowerInvariant() switch
        {
            "qty" => rows.OrderByDescending(r => r.Qty),
            "margin" => rows.OrderByDescending(r => r.Margin),
            _ => rows.OrderByDescending(r => r.Revenue)
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
