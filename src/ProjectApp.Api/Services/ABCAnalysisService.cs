using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Services;

public enum ABCCategory
{
    A = 1,  // Ведущие товары (80% выручки)
    B = 2,  // Средние товары (15% выручки)
    C = 3   // Отстающие товары (5% выручки)
}

public class ProductABCResult
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal TotalRevenue { get; set; }
    public decimal TotalQty { get; set; }
    public int SalesCount { get; set; }
    public decimal RevenuePercent { get; set; }
    public decimal CumulativePercent { get; set; }
    public ABCCategory Category { get; set; }
    public decimal TurnoverDays { get; set; }  // Оборачиваемость в днях
}

public class ABCAnalysisService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ABCAnalysisService> _logger;

    public ABCAnalysisService(AppDbContext db, ILogger<ABCAnalysisService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Провести ABC-анализ товаров за период
    /// </summary>
    public async Task<List<ProductABCResult>> AnalyzeAsync(DateTime dateFrom, DateTime dateTo, CancellationToken ct = default)
    {
        _logger.LogInformation("[ABCAnalysis] Analyzing products from {From} to {To}", dateFrom, dateTo);

        // Получаем данные по продажам
        var salesData = await _db.SaleItems
            .Include(si => si.Sale)
            .Where(si => si.Sale.CreatedAt >= dateFrom && si.Sale.CreatedAt < dateTo)
            .GroupBy(si => si.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                TotalRevenue = g.Sum(si => si.Qty * si.UnitPrice),
                TotalQty = g.Sum(si => si.Qty),
                SalesCount = g.Count()
            })
            .OrderByDescending(g => g.TotalRevenue)
            .ToListAsync(ct);

        if (!salesData.Any())
        {
            _logger.LogWarning("[ABCAnalysis] No sales data for the period");
            return new List<ProductABCResult>();
        }

        var totalRevenue = salesData.Sum(s => s.TotalRevenue);

        // Получаем информацию о товарах
        var productIds = salesData.Select(s => s.ProductId).ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        // Рассчитываем оборачиваемость
        var days = (dateTo - dateFrom).Days;
        var stocks = await _db.Stocks
            .Where(s => productIds.Contains(s.ProductId))
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, TotalQty = g.Sum(s => s.Qty) })
            .ToDictionaryAsync(s => s.ProductId, s => s.TotalQty, ct);

        var results = new List<ProductABCResult>();
        decimal cumulativePercent = 0m;

        foreach (var data in salesData)
        {
            if (!products.TryGetValue(data.ProductId, out var product))
                continue;

            var revenuePercent = totalRevenue > 0 ? (data.TotalRevenue / totalRevenue) * 100 : 0;
            cumulativePercent += revenuePercent;

            // Расчет оборачиваемости
            var currentStock = stocks.TryGetValue(data.ProductId, out var qty) ? qty : 0;
            var turnoverDays = data.TotalQty > 0 && days > 0
                ? (currentStock / (data.TotalQty / days))
                : 0;

            results.Add(new ProductABCResult
            {
                ProductId = data.ProductId,
                Sku = product.Sku,
                Name = product.Name,
                TotalRevenue = data.TotalRevenue,
                TotalQty = data.TotalQty,
                SalesCount = data.SalesCount,
                RevenuePercent = decimal.Round(revenuePercent, 2),
                CumulativePercent = decimal.Round(cumulativePercent, 2),
                Category = DetermineCategory(cumulativePercent),
                TurnoverDays = decimal.Round(turnoverDays, 1)
            });
        }

        _logger.LogInformation("[ABCAnalysis] Analyzed {Count} products", results.Count);

        var categoryStats = results.GroupBy(r => r.Category)
            .Select(g => new { Category = g.Key, Count = g.Count(), Revenue = g.Sum(r => r.TotalRevenue) })
            .ToList();

        foreach (var stat in categoryStats)
        {
            _logger.LogInformation(
                "[ABCAnalysis] Category {Category}: {Count} products, {Revenue:N0} revenue",
                stat.Category, stat.Count, stat.Revenue);
        }

        return results;
    }

    private static ABCCategory DetermineCategory(decimal cumulativePercent)
    {
        if (cumulativePercent <= 80) return ABCCategory.A;
        if (cumulativePercent <= 95) return ABCCategory.B;
        return ABCCategory.C;
    }

    /// <summary>
    /// Получить рекомендации по товарам категории C
    /// </summary>
    public async Task<List<string>> GetRecommendationsAsync(DateTime dateFrom, DateTime dateTo, CancellationToken ct = default)
    {
        var analysis = await AnalyzeAsync(dateFrom, dateTo, ct);
        var recommendations = new List<string>();

        var categoryC = analysis.Where(a => a.Category == ABCCategory.C).ToList();

        if (categoryC.Any())
        {
            recommendations.Add($"Категория C содержит {categoryC.Count} товаров с низкой оборачиваемостью");

            var slowMovers = categoryC.Where(p => p.TurnoverDays > 90).ToList();
            if (slowMovers.Any())
            {
                recommendations.Add($"❗ {slowMovers.Count} товаров не продаются более 90 дней - рекомендуется акция или списание");
            }

            var zeroTurnover = categoryC.Where(p => p.TurnoverDays == 0 || p.TurnoverDays > 180).Take(10).ToList();
            foreach (var product in zeroTurnover)
            {
                recommendations.Add($"  • {product.Sku} ({product.Name}) - оборачиваемость {product.TurnoverDays:N0} дней");
            }
        }

        return recommendations;
    }
}
