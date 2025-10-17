using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Services;

public class PromotionService
{
    private readonly AppDbContext _db;
    private readonly ABCAnalysisService _abcAnalysis;
    private readonly DemandForecastService _forecast;
    private readonly ILogger<PromotionService> _logger;

    public PromotionService(
        AppDbContext db,
        ABCAnalysisService abcAnalysis,
        DemandForecastService forecast,
        ILogger<PromotionService> logger)
    {
        _db = db;
        _abcAnalysis = abcAnalysis;
        _forecast = forecast;
        _logger = logger;
    }

    /// <summary>
    /// Автоматически создать акции на основе анализа
    /// </summary>
    public async Task<List<Promotion>> AutoGeneratePromotionsAsync(string createdBy, CancellationToken ct = default)
    {
        _logger.LogInformation("[PromotionService] Auto-generating promotions");

        var promotions = new List<Promotion>();

        // 1. Акция на неликвиды (категория C + медленная оборачиваемость)
        var slowMovers = await GetSlowMovingProductsAsync(ct);
        if (slowMovers.Any())
        {
            var clearancePromo = new Promotion
            {
                Name = "Распродажа неликвидов",
                Description = "Товары категории C с низкой оборачиваемостью",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(30),
                Type = PromotionType.Clearance,
                DiscountPercent = 30m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy,
                Note = "Автоматически создано на основе ABC-анализа",
                Items = slowMovers.Select(p => new PromotionItem
                {
                    ProductId = p,
                    CustomDiscountPercent = null
                }).ToList()
            };
            promotions.Add(clearancePromo);
        }

        // 2. Акция на товары с падающим спросом
        var decliningProducts = await GetDecliningDemandProductsAsync(ct);
        if (decliningProducts.Any())
        {
            var trendPromo = new Promotion
            {
                Name = "Скидка на товары со снижением спроса",
                Description = "Товары с отрицательным трендом продаж",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(14),
                Type = PromotionType.PercentDiscount,
                DiscountPercent = 15m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy,
                Note = "Автоматически создано на основе прогноза спроса",
                Items = decliningProducts.Select(p => new PromotionItem
                {
                    ProductId = p,
                    CustomDiscountPercent = null
                }).ToList()
            };
            promotions.Add(trendPromo);
        }

        // 3. Акция на избыток товара (много на складе + низкий спрос)
        var overstock = await GetOverstockedProductsAsync(ct);
        if (overstock.Any())
        {
            var overstockPromo = new Promotion
            {
                Name = "Распродажа избыточных товаров",
                Description = "Товары с большими остатками и низким спросом",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(21),
                Type = PromotionType.PercentDiscount,
                DiscountPercent = 20m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy,
                Note = "Автоматически создано для сокращения остатков",
                Items = overstock.Select(p => new PromotionItem
                {
                    ProductId = p,
                    CustomDiscountPercent = null
                }).ToList()
            };
            promotions.Add(overstockPromo);
        }

        foreach (var promo in promotions)
        {
            _db.Promotions.Add(promo);
            _logger.LogInformation(
                "[PromotionService] Created promotion: {Name} with {Count} items",
                promo.Name, promo.Items.Count);
        }

        await _db.SaveChangesAsync(ct);

        return promotions;
    }

    /// <summary>
    /// Товары категории C с оборачиваемостью > 90 дней
    /// </summary>
    private async Task<List<int>> GetSlowMovingProductsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var last90Days = now.AddDays(-90);

        var abcResults = await _abcAnalysis.AnalyzeAsync(last90Days, now, ct);

        return abcResults
            .Where(r => r.Category == ABCCategory.C && r.TurnoverDays > 90)
            .Select(r => r.ProductId)
            .ToList();
    }

    /// <summary>
    /// Товары с падающим спросом (отрицательный тренд)
    /// </summary>
    private async Task<List<int>> GetDecliningDemandProductsAsync(CancellationToken ct)
    {
        var forecast = await _forecast.ForecastAsync(30, ct);

        return forecast
            .Where(f => f.Trend < 0 && f.AverageDailySales > 0)
            .Select(f => f.ProductId)
            .Take(20)
            .ToList();
    }

    /// <summary>
    /// Товары с избытком на складе (запас > 60 дней)
    /// </summary>
    private async Task<List<int>> GetOverstockedProductsAsync(CancellationToken ct)
    {
        var forecast = await _forecast.ForecastAsync(30, ct);

        return forecast
            .Where(f => f.DaysUntilStockout > 60 && f.CurrentStock > 0)
            .Select(f => f.ProductId)
            .Take(15)
            .ToList();
    }

    /// <summary>
    /// Получить активные акции
    /// </summary>
    public async Task<List<Promotion>> GetActivePromotionsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _db.Promotions
            .Include(p => p.Items)
            .Where(p => p.IsActive && p.StartDate <= now && p.EndDate >= now)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Деактивировать истекшие акции
    /// </summary>
    public async Task DeactivateExpiredPromotionsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expired = await _db.Promotions
            .Where(p => p.IsActive && p.EndDate < now)
            .ToListAsync(ct);

        foreach (var promo in expired)
        {
            promo.IsActive = false;
            _logger.LogInformation("[PromotionService] Deactivated expired promotion: {Name}", promo.Name);
        }

        await _db.SaveChangesAsync(ct);
    }
}
