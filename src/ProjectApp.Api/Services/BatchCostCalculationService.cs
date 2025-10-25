using ProjectApp.Api.Data;
using ProjectApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ProjectApp.Api.Services;

/// <summary>
/// Сервис расчета себестоимости партий товаров
/// </summary>
public class BatchCostCalculationService
{
    private readonly AppDbContext _db;

    public BatchCostCalculationService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Получить или создать настройки для поставки
    /// </summary>
    public async Task<BatchCostSettings> GetOrCreateSettingsAsync(int supplyId)
    {
        var settings = await _db.BatchCostSettings
            .FirstOrDefaultAsync(s => s.SupplyId == supplyId);

        if (settings == null)
        {
            settings = new BatchCostSettings
            {
                SupplyId = supplyId,
                ExchangeRate = 158.08m,
                CustomsFixedTotal = 0,
                ShippingFixedTotal = 0,
                DefaultVatPercent = 0,
                DefaultLogisticsPercent = 0,
                DefaultWarehousePercent = 0,
                DefaultDeclarationPercent = 0,
                DefaultCertificationPercent = 0,
                DefaultMchsPercent = 0,
                DefaultDeviationPercent = 0,
                CreatedAt = DateTime.UtcNow
            };

            _db.BatchCostSettings.Add(settings);
            await _db.SaveChangesAsync();
        }

        return settings;
    }

    /// <summary>
    /// Обновить настройки
    /// </summary>
    public async Task UpdateSettingsAsync(BatchCostSettings settings)
    {
        settings.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Добавить товар в расчет
    /// </summary>
    public async Task<BatchCostCalculation> AddItemAsync(int supplyId, int? batchId, string productName, 
        int quantity, decimal priceRub, BatchCostSettings settings, string createdBy)
    {
        var item = new BatchCostCalculation
        {
            SupplyId = supplyId,
            BatchId = batchId,
            ProductName = productName,
            Quantity = quantity,
            PriceRub = priceRub,
            ExchangeRate = settings.ExchangeRate,
            VatPercent = settings.DefaultVatPercent,
            LogisticsPercent = settings.DefaultLogisticsPercent,
            WarehousePercent = settings.DefaultWarehousePercent,
            DeclarationPercent = settings.DefaultDeclarationPercent,
            CertificationPercent = settings.DefaultCertificationPercent,
            MchsPercent = settings.DefaultMchsPercent,
            DeviationPercent = settings.DefaultDeviationPercent,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        // Рассчитываем
        CalculateItem(item);

        _db.BatchCostCalculations.Add(item);
        await _db.SaveChangesAsync();

        return item;
    }

    /// <summary>
    /// Пересчитать все товары поставки
    /// </summary>
    public async Task RecalculateAllAsync(int supplyId)
    {
        var settings = await GetOrCreateSettingsAsync(supplyId);
        
        // Получаем все товары этой поставки
        var items = await _db.BatchCostCalculations
            .Where(c => c.SupplyId == supplyId)
            .ToListAsync();

        if (!items.Any())
            return;

        // Общая цена в сумах для расчета долей
        var totalPriceSom = items.Sum(i => i.PriceSom);
        var totalQuantity = items.Sum(i => i.Quantity);

        foreach (var item in items)
        {
            // Базовая цена в сумах
            item.PriceSom = item.PriceRub * item.ExchangeRate;

            // Доля таможни (фикс. сумма делится пропорционально)
            item.CustomsAmount = totalPriceSom > 0 
                ? (item.PriceSom / totalPriceSom) * settings.CustomsFixedTotal 
                : 0;

            // Доля погрузки (фикс. сумма делится пропорционально)
            item.ShippingAmount = totalPriceSom > 0 
                ? (item.PriceSom / totalPriceSom) * settings.ShippingFixedTotal 
                : 0;

            // Расчет процентных статей
            item.LogisticsAmount = item.PriceSom * (item.LogisticsPercent / 100);
            item.WarehouseAmount = item.PriceSom * (item.WarehousePercent / 100);
            item.DeclarationAmount = item.PriceSom * (item.DeclarationPercent / 100);
            item.CertificationAmount = item.PriceSom * (item.CertificationPercent / 100);
            item.MchsAmount = item.PriceSom * (item.MchsPercent / 100);
            item.DeviationAmount = item.PriceSom * (item.DeviationPercent / 100);

            // Себестоимость за единицу
            item.UnitCost = item.PriceSom 
                + item.CustomsAmount 
                + item.LogisticsAmount 
                + item.WarehouseAmount 
                + item.DeclarationAmount 
                + item.CertificationAmount 
                + item.MchsAmount 
                + item.ShippingAmount 
                + item.DeviationAmount;

            // Итого за весь товар
            item.TotalCost = item.UnitCost * item.Quantity;
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Получить все расчеты для поставки
    /// </summary>
    public async Task<List<BatchCostCalculation>> GetItemsBySupplyAsync(int supplyId)
    {
        return await _db.BatchCostCalculations
            .Where(c => c.SupplyId == supplyId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Получить общую себестоимость партии
    /// </summary>
    public async Task<decimal> GetTotalCostAsync(int supplyId)
    {
        return await _db.BatchCostCalculations
            .Where(c => c.SupplyId == supplyId)
            .SumAsync(c => c.TotalCost);
    }

    /// <summary>
    /// Удалить товар из расчета
    /// </summary>
    public async Task DeleteItemAsync(int itemId, int supplyId)
    {
        var item = await _db.BatchCostCalculations.FindAsync(itemId);
        if (item != null)
        {
            _db.BatchCostCalculations.Remove(item);
            await _db.SaveChangesAsync();
            
            // Пересчитываем оставшиеся
            await RecalculateAllAsync(supplyId);
        }
    }

    /// <summary>
    /// Рассчитать один товар (без сохранения)
    /// </summary>
    private void CalculateItem(BatchCostCalculation item)
    {
        // Цена в сумах
        item.PriceSom = item.PriceRub * item.ExchangeRate;

        // Проценты от цены в сумах
        item.LogisticsAmount = item.PriceSom * (item.LogisticsPercent / 100);
        item.WarehouseAmount = item.PriceSom * (item.WarehousePercent / 100);
        item.DeclarationAmount = item.PriceSom * (item.DeclarationPercent / 100);
        item.CertificationAmount = item.PriceSom * (item.CertificationPercent / 100);
        item.MchsAmount = item.PriceSom * (item.MchsPercent / 100);
        item.DeviationAmount = item.PriceSom * (item.DeviationPercent / 100);

        // Себестоимость (фикс. суммы пока 0, обновятся при RecalculateAll)
        item.UnitCost = item.PriceSom 
            + item.CustomsAmount 
            + item.LogisticsAmount 
            + item.WarehouseAmount 
            + item.DeclarationAmount 
            + item.CertificationAmount 
            + item.MchsAmount 
            + item.ShippingAmount 
            + item.DeviationAmount;

        item.TotalCost = item.UnitCost * item.Quantity;
    }
}
