using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Services;

/// <summary>
/// Сервис управления операционными расходами
/// </summary>
public class OperatingExpensesService
{
    private readonly AppDbContext _db;
    private readonly CashboxService _cashboxService;

    public OperatingExpensesService(AppDbContext db, CashboxService cashboxService)
    {
        _db = db;
        _cashboxService = cashboxService;
    }

    /// <summary>
    /// Создать операционный расход
    /// </summary>
    public async Task<OperatingExpense> CreateExpenseAsync(OperatingExpense expense, bool createCashTransaction = true)
    {
        expense.CreatedAt = DateTime.UtcNow;
        
        if (expense.PaymentStatus == ExpensePaymentStatus.Paid && !expense.PaidAt.HasValue)
        {
            expense.PaidAt = DateTime.UtcNow;
        }

        _db.OperatingExpenses.Add(expense);
        await _db.SaveChangesAsync();

        // Если расход оплачен и указана касса - создать транзакцию
        if (createCashTransaction && 
            expense.PaymentStatus == ExpensePaymentStatus.Paid && 
            expense.CashboxId.HasValue)
        {
            var transaction = new CashTransaction
            {
                Type = CashTransactionType.Expense,
                FromCashboxId = expense.CashboxId,
                Amount = expense.Amount,
                Currency = expense.Currency,
                Category = expense.Type.ToString(),
                Description = $"Операционный расход: {expense.Description}",
                LinkedExpenseId = expense.Id,
                CreatedBy = expense.CreatedBy,
                Status = TransactionStatus.Completed
            };

            await _cashboxService.CreateTransactionAsync(transaction);
        }

        return expense;
    }

    /// <summary>
    /// Получить список расходов
    /// </summary>
    public async Task<List<OperatingExpense>> GetExpensesAsync(
        DateTime? from = null,
        DateTime? to = null,
        ExpenseType? type = null,
        ExpensePaymentStatus? status = null,
        int? cashboxId = null)
    {
        var query = _db.OperatingExpenses
            .Include(e => e.Cashbox)
            .AsQueryable();

        if (from.HasValue)
        {
            query = query.Where(e => e.ExpenseDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.ExpenseDate <= to.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(e => e.Type == type.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(e => e.PaymentStatus == status.Value);
        }

        if (cashboxId.HasValue)
        {
            query = query.Where(e => e.CashboxId == cashboxId.Value);
        }

        return await query.OrderByDescending(e => e.ExpenseDate).ToListAsync();
    }

    /// <summary>
    /// Получить расход по ID
    /// </summary>
    public async Task<OperatingExpense?> GetExpenseByIdAsync(int id)
    {
        return await _db.OperatingExpenses
            .Include(e => e.Cashbox)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    /// <summary>
    /// Пометить расход как оплаченный
    /// </summary>
    public async Task MarkAsPaidAsync(int expenseId, int cashboxId, string paidBy)
    {
        var expense = await GetExpenseByIdAsync(expenseId);
        if (expense == null)
        {
            throw new InvalidOperationException("Расход не найден");
        }

        if (expense.PaymentStatus == ExpensePaymentStatus.Paid)
        {
            throw new InvalidOperationException("Расход уже оплачен");
        }

        expense.PaymentStatus = ExpensePaymentStatus.Paid;
        expense.PaidAt = DateTime.UtcNow;
        expense.CashboxId = cashboxId;

        // Создать транзакцию
        var transaction = new CashTransaction
        {
            Type = CashTransactionType.Expense,
            FromCashboxId = cashboxId,
            Amount = expense.Amount,
            Currency = expense.Currency,
            Category = expense.Type.ToString(),
            Description = $"Оплата расхода: {expense.Description}",
            LinkedExpenseId = expense.Id,
            CreatedBy = paidBy,
            Status = TransactionStatus.Completed
        };

        await _cashboxService.CreateTransactionAsync(transaction);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Получить сумму расходов за период
    /// </summary>
    public async Task<decimal> GetTotalExpensesAsync(DateTime from, DateTime to, ExpenseType? type = null)
    {
        var query = _db.OperatingExpenses
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to && e.PaymentStatus == ExpensePaymentStatus.Paid);

        if (type.HasValue)
        {
            query = query.Where(e => e.Type == type.Value);
        }

        return await query.SumAsync(e => (decimal?)e.Amount) ?? 0m;
    }

    /// <summary>
    /// Получить расходы по типам (для аналитики)
    /// </summary>
    public async Task<Dictionary<ExpenseType, decimal>> GetExpensesByTypeAsync(DateTime from, DateTime to)
    {
        return await _db.OperatingExpenses
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to && e.PaymentStatus == ExpensePaymentStatus.Paid)
            .GroupBy(e => e.Type)
            .Select(g => new { Type = g.Key, Total = g.Sum(e => e.Amount) })
            .ToDictionaryAsync(x => x.Type, x => x.Total);
    }

    /// <summary>
    /// Получить регулярные расходы
    /// </summary>
    public async Task<List<OperatingExpense>> GetRecurringExpensesAsync()
    {
        return await _db.OperatingExpenses
            .Where(e => e.IsRecurring)
            .OrderBy(e => e.Type)
            .ToListAsync();
    }
}
