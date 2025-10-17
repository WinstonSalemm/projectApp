using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Services;

public class DiscountValidationResult
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public decimal MaxAllowedDiscount { get; set; }
    public decimal ProfitMarginAfterDiscount { get; set; }
}

public class DiscountValidationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<DiscountValidationService> _logger;

    // Минимальная маржа после скидки (%)
    private const decimal MIN_PROFIT_MARGIN = 5m;
    
    // Максимальная скидка по типу клиента (%)
    private static readonly Dictionary<ClientType, decimal> MaxDiscountByClientType = new()
    {
        { ClientType.Individual, 5m },
        { ClientType.Company, 10m },
        { ClientType.Retail, 10m },
        { ClientType.Wholesale, 15m },
        { ClientType.LargeWholesale, 20m }
    };

    public DiscountValidationService(AppDbContext db, ILogger<DiscountValidationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Валидировать скидку для продажи
    /// </summary>
    public async Task<DiscountValidationResult> ValidateDiscountAsync(
        int productId,
        decimal unitPrice,
        decimal discountPercent,
        int? clientId = null,
        CancellationToken ct = default)
    {
        var product = await _db.Products.FindAsync(new object[] { productId }, ct);
        if (product == null)
        {
            return new DiscountValidationResult
            {
                IsValid = false,
                Message = "Товар не найден"
            };
        }

        // Получаем себестоимость (FIFO)
        var avgCost = await GetAverageCostAsync(productId, ct);
        if (avgCost == 0)
        {
            _logger.LogWarning("[DiscountValidation] Product {ProductId} has zero cost", productId);
            avgCost = product.Cost; // Фолбэк на базовую себестоимость
        }

        // Цена после скидки
        var priceAfterDiscount = unitPrice * (1 - discountPercent / 100);

        // Маржа после скидки
        var profitMargin = avgCost > 0 ? ((priceAfterDiscount - avgCost) / priceAfterDiscount) * 100 : 0;

        // Максимальная скидка по типу клиента
        decimal maxAllowedByClient = 10m; // По умолчанию
        if (clientId.HasValue)
        {
            var client = await _db.Clients.FindAsync(new object[] { clientId.Value }, ct);
            if (client != null && MaxDiscountByClientType.TryGetValue(client.Type, out var max))
            {
                maxAllowedByClient = max;
            }
        }

        // Максимальная скидка по марже (чтобы не уйти ниже MIN_PROFIT_MARGIN)
        var maxAllowedByMargin = 100m;
        if (avgCost > 0 && unitPrice > avgCost)
        {
            // Рассчитываем максимальную скидку при которой остается MIN_PROFIT_MARGIN
            // priceAfterDiscount = avgCost / (1 - MIN_PROFIT_MARGIN/100)
            var minPrice = avgCost / (1 - MIN_PROFIT_MARGIN / 100);
            maxAllowedByMargin = ((unitPrice - minPrice) / unitPrice) * 100;
        }

        var maxAllowed = Math.Min(maxAllowedByClient, maxAllowedByMargin);
        maxAllowed = Math.Max(0, maxAllowed); // Не отрицательная

        var isValid = discountPercent <= maxAllowed && profitMargin >= MIN_PROFIT_MARGIN;

        var message = isValid
            ? "Скидка допустима"
            : profitMargin < MIN_PROFIT_MARGIN
                ? $"Скидка приведет к маржинальности {profitMargin:N1}% (мин. {MIN_PROFIT_MARGIN}%)"
                : $"Скидка превышает лимит {maxAllowed:N1}% для данного типа клиента";

        return new DiscountValidationResult
        {
            IsValid = isValid,
            Message = message,
            MaxAllowedDiscount = decimal.Round(maxAllowed, 1),
            ProfitMarginAfterDiscount = decimal.Round(profitMargin, 1)
        };
    }

    /// <summary>
    /// Получить среднюю себестоимость по FIFO
    /// </summary>
    private async Task<decimal> GetAverageCostAsync(int productId, CancellationToken ct)
    {
        var batches = await _db.Batches
            .Where(b => b.ProductId == productId && b.Qty > 0)
            .OrderBy(b => b.CreatedAt)
            .Take(5) // Берем первые 5 партий (FIFO)
            .ToListAsync(ct);

        if (!batches.Any())
            return 0;

        var totalCost = batches.Sum(b => b.Qty * b.UnitCost);
        var totalQty = batches.Sum(b => b.Qty);

        return totalQty > 0 ? totalCost / totalQty : 0;
    }

    /// <summary>
    /// Получить рекомендуемую цену с учетом минимальной маржи
    /// </summary>
    public async Task<decimal> GetRecommendedPriceAsync(int productId, decimal targetMargin = 15m, CancellationToken ct = default)
    {
        var avgCost = await GetAverageCostAsync(productId, ct);
        if (avgCost == 0)
        {
            var product = await _db.Products.FindAsync(new object[] { productId }, ct);
            avgCost = product?.Cost ?? 0;
        }

        if (avgCost == 0)
            return 0;

        // Цена = Себестоимость / (1 - Маржа/100)
        return avgCost / (1 - targetMargin / 100);
    }

    /// <summary>
    /// Анализ скидок за период (упрощенный - на основе разницы цены и себестоимости)
    /// </summary>
    public async Task<Dictionary<string, object>> AnalyzeDiscountsAsync(DateTime dateFrom, DateTime dateTo, CancellationToken ct = default)
    {
        var salesItems = await _db.SaleItems
            .Where(si => _db.Sales.Any(s => s.Id == si.SaleId && s.CreatedAt >= dateFrom && s.CreatedAt < dateTo))
            .ToListAsync(ct);

        var totalItems = salesItems.Count;
        
        // Считаем потенциальную скидку по разнице между ценой продажи и себестоимостью
        var totalRevenue = salesItems.Sum(si => si.Qty * si.UnitPrice);
        var totalCost = salesItems.Sum(si => si.Qty * si.Cost);
        var totalMargin = totalRevenue - totalCost;
        var avgMarginPercent = totalRevenue > 0 ? (totalMargin / totalRevenue) * 100 : 0;

        return new Dictionary<string, object>
        {
            { "TotalItems", totalItems },
            { "TotalRevenue", decimal.Round(totalRevenue, 2) },
            { "TotalCost", decimal.Round(totalCost, 2) },
            { "TotalMargin", decimal.Round(totalMargin, 2) },
            { "AverageMarginPercent", decimal.Round(avgMarginPercent, 2) }
        };
    }
}
