using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Services;

/// <summary>
/// Сервис управления кассами и денежными транзакциями
/// </summary>
public class CashboxService
{
    private readonly AppDbContext _db;

    public CashboxService(AppDbContext db)
    {
        _db = db;
    }

    // ====== Кассы ======

    /// <summary>
    /// Получить все кассы
    /// </summary>
    public async Task<List<Cashbox>> GetAllCashboxesAsync(bool includeInactive = false)
    {
        var query = _db.Cashboxes.AsQueryable();
        
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }
        
        return await query.OrderBy(c => c.Type).ThenBy(c => c.Name).ToListAsync();
    }

    /// <summary>
    /// Получить кассу по ID
    /// </summary>
    public async Task<Cashbox?> GetCashboxByIdAsync(int id)
    {
        return await _db.Cashboxes.FindAsync(id);
    }

    /// <summary>
    /// Создать новую кассу
    /// </summary>
    public async Task<Cashbox> CreateCashboxAsync(Cashbox cashbox)
    {
        cashbox.CreatedAt = DateTime.UtcNow;
        _db.Cashboxes.Add(cashbox);
        await _db.SaveChangesAsync();
        return cashbox;
    }

    /// <summary>
    /// Обновить кассу
    /// </summary>
    public async Task<Cashbox> UpdateCashboxAsync(Cashbox cashbox)
    {
        cashbox.UpdatedAt = DateTime.UtcNow;
        _db.Cashboxes.Update(cashbox);
        await _db.SaveChangesAsync();
        return cashbox;
    }

    /// <summary>
    /// Деактивировать кассу (не удалять, а пометить как неактивную)
    /// </summary>
    public async Task DeactivateCashboxAsync(int id)
    {
        var cashbox = await GetCashboxByIdAsync(id);
        if (cashbox != null)
        {
            cashbox.IsActive = false;
            cashbox.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Получить общий баланс всех касс
    /// </summary>
    public async Task<Dictionary<string, decimal>> GetTotalBalancesByCurrencyAsync()
    {
        return await _db.Cashboxes
            .Where(c => c.IsActive)
            .GroupBy(c => c.Currency)
            .Select(g => new { Currency = g.Key, Total = g.Sum(c => c.CurrentBalance) })
            .ToDictionaryAsync(x => x.Currency, x => x.Total);
    }

    // ====== Транзакции ======

    /// <summary>
    /// Создать транзакцию (с автоматическим обновлением балансов)
    /// </summary>
    public async Task<CashTransaction> CreateTransactionAsync(CashTransaction transaction)
    {
        transaction.CreatedAt = DateTime.UtcNow;
        transaction.Status = TransactionStatus.Completed;

        // Обновление балансов в зависимости от типа транзакции
        switch (transaction.Type)
        {
            case CashTransactionType.Income:
                // Приход в кассу
                if (transaction.ToCashboxId.HasValue)
                {
                    var toCashbox = await GetCashboxByIdAsync(transaction.ToCashboxId.Value);
                    if (toCashbox != null)
                    {
                        toCashbox.CurrentBalance += transaction.Amount;
                        toCashbox.UpdatedAt = DateTime.UtcNow;
                    }
                }
                break;

            case CashTransactionType.Expense:
                // Расход из кассы
                if (transaction.FromCashboxId.HasValue)
                {
                    var fromCashbox = await GetCashboxByIdAsync(transaction.FromCashboxId.Value);
                    if (fromCashbox != null)
                    {
                        if (fromCashbox.CurrentBalance < transaction.Amount)
                        {
                            throw new InvalidOperationException($"Недостаточно средств в кассе {fromCashbox.Name}. Доступно: {fromCashbox.CurrentBalance}, требуется: {transaction.Amount}");
                        }
                        fromCashbox.CurrentBalance -= transaction.Amount;
                        fromCashbox.UpdatedAt = DateTime.UtcNow;
                    }
                }
                break;

            case CashTransactionType.Transfer:
                // Перемещение между кассами
                if (transaction.FromCashboxId.HasValue && transaction.ToCashboxId.HasValue)
                {
                    var fromCashbox = await GetCashboxByIdAsync(transaction.FromCashboxId.Value);
                    var toCashbox = await GetCashboxByIdAsync(transaction.ToCashboxId.Value);
                    
                    if (fromCashbox != null && toCashbox != null)
                    {
                        if (fromCashbox.CurrentBalance < transaction.Amount)
                        {
                            throw new InvalidOperationException($"Недостаточно средств в кассе {fromCashbox.Name}");
                        }
                        fromCashbox.CurrentBalance -= transaction.Amount;
                        toCashbox.CurrentBalance += transaction.Amount;
                        fromCashbox.UpdatedAt = DateTime.UtcNow;
                        toCashbox.UpdatedAt = DateTime.UtcNow;
                    }
                }
                break;

            case CashTransactionType.SalePayment:
                // Оплата от клиента
                if (transaction.ToCashboxId.HasValue)
                {
                    var toCashbox = await GetCashboxByIdAsync(transaction.ToCashboxId.Value);
                    if (toCashbox != null)
                    {
                        toCashbox.CurrentBalance += transaction.Amount;
                        toCashbox.UpdatedAt = DateTime.UtcNow;
                    }
                }
                break;

            case CashTransactionType.Withdrawal:
                // Инкассация (вывод из кассы)
                if (transaction.FromCashboxId.HasValue)
                {
                    var fromCashbox = await GetCashboxByIdAsync(transaction.FromCashboxId.Value);
                    if (fromCashbox != null)
                    {
                        if (fromCashbox.CurrentBalance < transaction.Amount)
                        {
                            throw new InvalidOperationException($"Недостаточно средств для инкассации");
                        }
                        fromCashbox.CurrentBalance -= transaction.Amount;
                        fromCashbox.UpdatedAt = DateTime.UtcNow;
                    }
                }
                break;
        }

        _db.CashTransactions.Add(transaction);
        await _db.SaveChangesAsync();
        return transaction;
    }

    /// <summary>
    /// Получить историю транзакций
    /// </summary>
    public async Task<List<CashTransaction>> GetTransactionsAsync(
        int? cashboxId = null,
        DateTime? from = null,
        DateTime? to = null,
        CashTransactionType? type = null,
        int page = 1,
        int pageSize = 50)
    {
        var query = _db.CashTransactions
            .Include(t => t.FromCashbox)
            .Include(t => t.ToCashbox)
            .AsQueryable();

        if (cashboxId.HasValue)
        {
            query = query.Where(t => t.FromCashboxId == cashboxId || t.ToCashboxId == cashboxId);
        }

        if (from.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(t => t.CreatedAt <= to.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(t => t.Type == type.Value);
        }

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    /// <summary>
    /// Отменить транзакцию (реверс)
    /// </summary>
    public async Task CancelTransactionAsync(int transactionId, string cancelledBy)
    {
        var transaction = await _db.CashTransactions.FindAsync(transactionId);
        if (transaction == null || transaction.Status == TransactionStatus.Cancelled)
        {
            return;
        }

        // Реверс балансов
        switch (transaction.Type)
        {
            case CashTransactionType.Income:
                if (transaction.ToCashboxId.HasValue)
                {
                    var toCashbox = await GetCashboxByIdAsync(transaction.ToCashboxId.Value);
                    if (toCashbox != null)
                    {
                        toCashbox.CurrentBalance -= transaction.Amount;
                    }
                }
                break;

            case CashTransactionType.Expense:
                if (transaction.FromCashboxId.HasValue)
                {
                    var fromCashbox = await GetCashboxByIdAsync(transaction.FromCashboxId.Value);
                    if (fromCashbox != null)
                    {
                        fromCashbox.CurrentBalance += transaction.Amount;
                    }
                }
                break;

            case CashTransactionType.Transfer:
                if (transaction.FromCashboxId.HasValue && transaction.ToCashboxId.HasValue)
                {
                    var fromCashbox = await GetCashboxByIdAsync(transaction.FromCashboxId.Value);
                    var toCashbox = await GetCashboxByIdAsync(transaction.ToCashboxId.Value);
                    
                    if (fromCashbox != null && toCashbox != null)
                    {
                        fromCashbox.CurrentBalance += transaction.Amount;
                        toCashbox.CurrentBalance -= transaction.Amount;
                    }
                }
                break;
        }

        transaction.Status = TransactionStatus.Cancelled;
        await _db.SaveChangesAsync();
    }
}
