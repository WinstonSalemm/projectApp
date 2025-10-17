using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;

namespace ProjectApp.Api.Services;

public class ProductForecast
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal AverageDailySales { get; set; }
    public decimal ForecastedDemand { get; set; }  // На следующие N дней
    public decimal RecommendedOrder { get; set; }   // Рекомендуемый заказ
    public int DaysUntilStockout { get; set; }      // Дней до исчерпания
    public decimal Trend { get; set; }              // Тренд (-1 = падение, 0 = стабильно, 1 = рост)
    public string Status { get; set; } = string.Empty;
}

public class DemandForecastService
{
    private readonly AppDbContext _db;
    private readonly ILogger<DemandForecastService> _logger;

    public DemandForecastService(AppDbContext db, ILogger<DemandForecastService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Прогноз спроса на N дней вперед
    /// </summary>
    public async Task<List<ProductForecast>> ForecastAsync(int forecastDays = 30, CancellationToken ct = default)
    {
        _logger.LogInformation("[DemandForecast] Forecasting demand for {Days} days", forecastDays);

        var now = DateTime.UtcNow;
        var last30Days = now.AddDays(-30);
        var last60Days = now.AddDays(-60);

        // Анализируем продажи за последние 30 и 60 дней
        var recentSales = await _db.SaleItems
            .Include(si => si.Sale)
            .Where(si => si.Sale.CreatedAt >= last30Days)
            .GroupBy(si => si.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                TotalQty30 = g.Sum(si => si.Qty),
                SalesCount = g.Count()
            })
            .ToListAsync(ct);

        var olderSales = await _db.SaleItems
            .Include(si => si.Sale)
            .Where(si => si.Sale.CreatedAt >= last60Days && si.Sale.CreatedAt < last30Days)
            .GroupBy(si => si.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                TotalQty30 = g.Sum(si => si.Qty)
            })
            .ToListAsync(ct);

        var olderSalesDict = olderSales.ToDictionary(s => s.ProductId, s => s.TotalQty30);

        // Получаем текущие остатки
        var stocks = await _db.Stocks
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, TotalQty = g.Sum(s => s.Qty) })
            .ToDictionaryAsync(s => s.ProductId, s => s.TotalQty, ct);

        // Получаем информацию о товарах
        var productIds = recentSales.Select(s => s.ProductId).ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var forecasts = new List<ProductForecast>();

        foreach (var sale in recentSales)
        {
            if (!products.TryGetValue(sale.ProductId, out var product))
                continue;

            var currentStock = stocks.TryGetValue(sale.ProductId, out var qty) ? qty : 0;

            // Среднедневные продажи за последние 30 дней
            var avgDailySales = sale.TotalQty30 / 30m;

            // Тренд: сравниваем последние 30 дней с предыдущими 30
            var prevSales = olderSalesDict.TryGetValue(sale.ProductId, out var prev) ? prev : 0;
            var trend = 0m;
            if (prevSales > 0)
            {
                var change = ((sale.TotalQty30 - prevSales) / prevSales) * 100;
                trend = change > 10 ? 1m : (change < -10 ? -1m : 0m);
            }

            // Прогноз с учетом тренда
            var forecastMultiplier = 1.0m + (trend * 0.2m); // ±20% при сильном тренде
            var forecastedDemand = avgDailySales * forecastDays * forecastMultiplier;

            // Дней до исчерпания
            var daysUntilStockout = avgDailySales > 0 ? (int)(currentStock / avgDailySales) : 999;

            // Рекомендуемый заказ (запас на прогноз + страховой запас 20%)
            var safetyStock = forecastedDemand * 0.2m;
            var recommendedOrder = Math.Max(0, forecastedDemand + safetyStock - currentStock);

            // Определяем статус
            var status = DetermineStatus(daysUntilStockout, avgDailySales, currentStock);

            forecasts.Add(new ProductForecast
            {
                ProductId = sale.ProductId,
                Sku = product.Sku,
                Name = product.Name,
                CurrentStock = currentStock,
                AverageDailySales = decimal.Round(avgDailySales, 2),
                ForecastedDemand = decimal.Round(forecastedDemand, 2),
                RecommendedOrder = decimal.Round(recommendedOrder, 2),
                DaysUntilStockout = daysUntilStockout,
                Trend = trend,
                Status = status
            });
        }

        // Сортируем по критичности
        forecasts = forecasts
            .OrderBy(f => f.DaysUntilStockout)
            .ThenByDescending(f => f.AverageDailySales)
            .ToList();

        _logger.LogInformation("[DemandForecast] Forecasted {Count} products", forecasts.Count);

        var critical = forecasts.Count(f => f.Status == "🔴 Критично");
        var warning = forecasts.Count(f => f.Status == "🟡 Внимание");
        var ok = forecasts.Count(f => f.Status == "🟢 Норма");

        _logger.LogInformation(
            "[DemandForecast] Status: {Critical} critical, {Warning} warning, {Ok} ok",
            critical, warning, ok);

        return forecasts;
    }

    private static string DetermineStatus(int daysUntilStockout, decimal avgDailySales, decimal currentStock)
    {
        if (avgDailySales == 0)
            return "⚪ Нет продаж";

        if (daysUntilStockout <= 7)
            return "🔴 Критично";

        if (daysUntilStockout <= 14)
            return "🟡 Внимание";

        if (currentStock == 0)
            return "⚫ Дефицит";

        return "🟢 Норма";
    }

    /// <summary>
    /// Получить товары с дефицитом
    /// </summary>
    public async Task<List<ProductForecast>> GetCriticalProductsAsync(CancellationToken ct = default)
    {
        var forecast = await ForecastAsync(30, ct);
        return forecast
            .Where(f => f.Status == "🔴 Критично" || f.Status == "⚫ Дефицит")
            .Take(20)
            .ToList();
    }

    /// <summary>
    /// Получить рекомендации по закупке
    /// </summary>
    public async Task<Dictionary<int, decimal>> GetPurchaseRecommendationsAsync(int forecastDays = 30, CancellationToken ct = default)
    {
        var forecast = await ForecastAsync(forecastDays, ct);
        
        return forecast
            .Where(f => f.RecommendedOrder > 0)
            .OrderByDescending(f => f.AverageDailySales)
            .ToDictionary(f => f.ProductId, f => f.RecommendedOrder);
    }
}
