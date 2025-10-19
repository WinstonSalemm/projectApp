using ProjectApp.Api.Data;
using ProjectApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ProjectApp.Api.Services;

/// <summary>
/// DTO для страницы "К инкассации"
/// </summary>
public class CashCollectionSummaryDto
{
    /// <summary>
    /// Накоплено с последней инкассации (серые продажи)
    /// </summary>
    public decimal CurrentAccumulated { get; set; }
    
    /// <summary>
    /// Дата последней инкассации
    /// </summary>
    public DateTime? LastCollectionDate { get; set; }
    
    /// <summary>
    /// Общий неинкассированный остаток (сумма всех RemainingAmount)
    /// </summary>
    public decimal TotalRemainingAmount { get; set; }
    
    /// <summary>
    /// История инкассаций
    /// </summary>
    public List<CashCollectionDto> History { get; set; } = new();
}

public class CashCollectionDto
{
    public int Id { get; set; }
    public DateTime CollectionDate { get; set; }
    public decimal AccumulatedAmount { get; set; }
    public decimal CollectedAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string? Notes { get; set; }
    public string? CreatedBy { get; set; }
}

public class CreateCashCollectionDto
{
    /// <summary>
    /// Сумма сданная при инкассации
    /// </summary>
    public decimal CollectedAmount { get; set; }
    
    /// <summary>
    /// Примечание
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Сервис для работы с инкассациями серых денег
/// </summary>
public class CashCollectionService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CashCollectionService> _logger;

    public CashCollectionService(AppDbContext db, ILogger<CashCollectionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Получить сводку для страницы "К инкассации"
    /// </summary>
    public async Task<CashCollectionSummaryDto> GetSummaryAsync()
    {
        // 1. Найти последнюю инкассацию
        var lastCollection = await _db.Set<CashCollection>()
            .OrderByDescending(c => c.CollectionDate)
            .FirstOrDefaultAsync();

        var lastCollectionDate = lastCollection?.CollectionDate ?? DateTime.MinValue;

        // 2. Рассчитать накопленную сумму с последней инкассации
        // Серые продажи = CashNoReceipt + Click (legacy) + ClickNoReceipt
        var greyPaymentTypes = new[] 
        { 
            PaymentType.CashNoReceipt, 
            PaymentType.Click, 
            PaymentType.ClickNoReceipt 
        };

        var currentAccumulated = await _db.Sales
            .Where(s => s.CreatedAt > lastCollectionDate && 
                       greyPaymentTypes.Contains(s.PaymentType))
            .SumAsync(s => (decimal?)s.Total) ?? 0m;

        // 3. Рассчитать общий неинкассированный остаток
        var totalRemaining = await _db.Set<CashCollection>()
            .SumAsync(c => (decimal?)c.RemainingAmount) ?? 0m;

        // 4. Получить историю инкассаций (последние 20)
        var history = await _db.Set<CashCollection>()
            .OrderByDescending(c => c.CollectionDate)
            .Take(20)
            .Select(c => new CashCollectionDto
            {
                Id = c.Id,
                CollectionDate = c.CollectionDate,
                AccumulatedAmount = c.AccumulatedAmount,
                CollectedAmount = c.CollectedAmount,
                RemainingAmount = c.RemainingAmount,
                Notes = c.Notes,
                CreatedBy = c.CreatedBy
            })
            .ToListAsync();

        return new CashCollectionSummaryDto
        {
            CurrentAccumulated = currentAccumulated,
            LastCollectionDate = lastCollection?.CollectionDate,
            TotalRemainingAmount = totalRemaining,
            History = history
        };
    }

    /// <summary>
    /// Провести инкассацию
    /// </summary>
    public async Task<CashCollectionDto> CreateCollectionAsync(CreateCashCollectionDto dto, string createdBy)
    {
        // 1. Рассчитать накопленную сумму с последней инкассации
        var lastCollection = await _db.Set<CashCollection>()
            .OrderByDescending(c => c.CollectionDate)
            .FirstOrDefaultAsync();

        var lastCollectionDate = lastCollection?.CollectionDate ?? DateTime.MinValue;

        var greyPaymentTypes = new[] 
        { 
            PaymentType.CashNoReceipt, 
            PaymentType.Click, 
            PaymentType.ClickNoReceipt 
        };

        var accumulatedAmount = await _db.Sales
            .Where(s => s.CreatedAt > lastCollectionDate && 
                       greyPaymentTypes.Contains(s.PaymentType))
            .SumAsync(s => (decimal?)s.Total) ?? 0m;

        // 2. Создать запись инкассации
        var collection = new CashCollection
        {
            CollectionDate = DateTime.UtcNow,
            AccumulatedAmount = accumulatedAmount,
            CollectedAmount = dto.CollectedAmount,
            RemainingAmount = accumulatedAmount - dto.CollectedAmount,
            Notes = dto.Notes,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        _db.Set<CashCollection>().Add(collection);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Cash collection created: Accumulated={Accumulated}, Collected={Collected}, Remaining={Remaining}, By={User}",
            accumulatedAmount, dto.CollectedAmount, collection.RemainingAmount, createdBy);

        return new CashCollectionDto
        {
            Id = collection.Id,
            CollectionDate = collection.CollectionDate,
            AccumulatedAmount = collection.AccumulatedAmount,
            CollectedAmount = collection.CollectedAmount,
            RemainingAmount = collection.RemainingAmount,
            Notes = collection.Notes,
            CreatedBy = collection.CreatedBy
        };
    }

    /// <summary>
    /// Получить историю инкассаций за период
    /// </summary>
    public async Task<List<CashCollectionDto>> GetHistoryAsync(DateTime? from = null, DateTime? to = null)
    {
        var query = _db.Set<CashCollection>().AsQueryable();

        if (from.HasValue)
            query = query.Where(c => c.CollectionDate >= from.Value);

        if (to.HasValue)
            query = query.Where(c => c.CollectionDate <= to.Value);

        return await query
            .OrderByDescending(c => c.CollectionDate)
            .Select(c => new CashCollectionDto
            {
                Id = c.Id,
                CollectionDate = c.CollectionDate,
                AccumulatedAmount = c.AccumulatedAmount,
                CollectedAmount = c.CollectedAmount,
                RemainingAmount = c.RemainingAmount,
                Notes = c.Notes,
                CreatedBy = c.CreatedBy
            })
            .ToListAsync();
    }

    /// <summary>
    /// Удалить инкассацию (только последнюю, если была ошибка)
    /// </summary>
    public async Task<bool> DeleteLastCollectionAsync()
    {
        var lastCollection = await _db.Set<CashCollection>()
            .OrderByDescending(c => c.CollectionDate)
            .FirstOrDefaultAsync();

        if (lastCollection == null)
            return false;

        _db.Set<CashCollection>().Remove(lastCollection);
        await _db.SaveChangesAsync();

        _logger.LogWarning("Cash collection deleted: Id={Id}, Date={Date}", 
            lastCollection.Id, lastCollection.CollectionDate);

        return true;
    }
}
