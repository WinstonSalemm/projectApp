using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Services;

/// <summary>
/// Дашборд владельца - ключевые метрики бизнеса
/// </summary>
public class OwnerDashboardService
{
    private readonly AppDbContext _db;
    private readonly CashboxService _cashboxService;
    private readonly OperatingExpensesService _expensesService;

    public OwnerDashboardService(
        AppDbContext db,
        CashboxService cashboxService,
        OperatingExpensesService expensesService)
    {
        _db = db;
        _cashboxService = cashboxService;
        _expensesService = expensesService;
    }

    /// <summary>
    /// Получить полный дашборд владельца
    /// </summary>
    public async Task<OwnerDashboardDto> GetDashboardAsync(DateTime? date = null)
    {
        var targetDate = date ?? DateTime.UtcNow.Date;
        var startOfDay = targetDate;
        var endOfDay = targetDate.AddDays(1);

        // Параллельные запросы для ускорения
        var tasksToday = new[]
        {
            GetTodayRevenueAsync(startOfDay, endOfDay),
            GetTodayProfitAsync(startOfDay, endOfDay),
            GetTodaySalesCountAsync(startOfDay, endOfDay),
            GetTodayAverageCheckAsync(startOfDay, endOfDay),
            GetTop5ProductsTodayAsync(startOfDay, endOfDay),
        };

        var tasksGeneral = new[]
        {
            _cashboxService.GetTotalBalancesByCurrencyAsync(),
            GetTotalClientDebtsAsync(),
            GetTotalSupplierDebtsAsync(),
            GetInventoryValueAsync(),
            GetCriticalStockAlertsAsync(),
            GetOverdueDebtsAsync(),
        };

        await Task.WhenAll(tasksToday.Concat(tasksGeneral));

        var todayRevenue = tasksToday[0].Result;
        var todayProfit = tasksToday[1].Result;
        var todaySalesCount = tasksToday[2].Result;
        var todayAvgCheck = tasksToday[3].Result;
        var top5Products = tasksToday[4].Result;

        var cashboxBalances = tasksGeneral[0].Result;
        var clientDebts = tasksGeneral[1].Result;
        var supplierDebts = tasksGeneral[2].Result;
        var inventoryValue = tasksGeneral[3].Result;
        var criticalStock = tasksGeneral[4].Result;
        var overdueDebts = tasksGeneral[5].Result;

        return new OwnerDashboardDto
        {
            // Финансы за сегодня
            TodayRevenue = todayRevenue,
            TodayProfit = todayProfit,
            TodaySalesCount = todaySalesCount,
            TodayAverageCheck = todayAvgCheck,

            // Кассы
            CashboxBalances = cashboxBalances,
            TotalCash = cashboxBalances.Values.Sum(),

            // Долги
            ClientDebts = clientDebts,
            SupplierDebts = supplierDebts,

            // Склад
            InventoryValue = inventoryValue,

            // Топ продукты сегодня
            Top5ProductsToday = top5Products,

            // Алерты
            CriticalStockAlerts = criticalStock,
            OverdueDebts = overdueDebts,

            GeneratedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Выручка за сегодня
    /// </summary>
    private async Task<decimal> GetTodayRevenueAsync(DateTime from, DateTime to)
    {
        return await _db.Sales
            .Where(s => s.CreatedAt >= from && s.CreatedAt < to)
            .SumAsync(s => s.Total);
    }

    /// <summary>
    /// Прибыль за сегодня (с учетом расходов)
    /// </summary>
    private async Task<decimal> GetTodayProfitAsync(DateTime from, DateTime to)
    {
        // Выручка
        var revenue = await GetTodayRevenueAsync(from, to);

        // Себестоимость проданных товаров
        var cogs = await _db.SaleItems
            .Where(si => si.Sale!.CreatedAt >= from && si.Sale.CreatedAt < to)
            .SumAsync(si => si.Quantity * si.CostPrice);

        // Операционные расходы за день
        var expenses = await _expensesService.GetTotalExpensesAsync(from, to);

        return revenue - cogs - expenses;
    }

    /// <summary>
    /// Количество продаж за сегодня
    /// </summary>
    private async Task<int> GetTodaySalesCountAsync(DateTime from, DateTime to)
    {
        return await _db.Sales
            .Where(s => s.CreatedAt >= from && s.CreatedAt < to)
            .CountAsync();
    }

    /// <summary>
    /// Средний чек за сегодня
    /// </summary>
    private async Task<decimal> GetTodayAverageCheckAsync(DateTime from, DateTime to)
    {
        var count = await GetTodaySalesCountAsync(from, to);
        if (count == 0) return 0;

        var total = await GetTodayRevenueAsync(from, to);
        return total / count;
    }

    /// <summary>
    /// Топ-5 товаров за сегодня по выручке
    /// </summary>
    private async Task<List<TopProductDto>> GetTop5ProductsTodayAsync(DateTime from, DateTime to)
    {
        return await _db.SaleItems
            .Where(si => si.Sale!.CreatedAt >= from && si.Sale.CreatedAt < to)
            .GroupBy(si => new { si.ProductId, si.ProductName })
            .Select(g => new TopProductDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                TotalRevenue = g.Sum(si => si.Price * si.Quantity),
                TotalQuantity = g.Sum(si => si.Quantity)
            })
            .OrderByDescending(p => p.TotalRevenue)
            .Take(5)
            .ToListAsync();
    }

    /// <summary>
    /// Общая сумма долгов клиентов
    /// </summary>
    private async Task<decimal> GetTotalClientDebtsAsync()
    {
        var totalDebts = await _db.Debts.SumAsync(d => (decimal?)d.Amount) ?? 0m;
        var paidDebts = await _db.DebtPayments.SumAsync(p => (decimal?)p.Amount) ?? 0m;
        return totalDebts - paidDebts;
    }

    /// <summary>
    /// Общая сумма долгов поставщикам
    /// </summary>
    private async Task<decimal> GetTotalSupplierDebtsAsync()
    {
        return await _db.Purchases
            .Where(p => p.PaidAt == null)
            .SumAsync(p => p.TotalAmount);
    }

    /// <summary>
    /// Стоимость товара на складе (по себестоимости)
    /// </summary>
    private async Task<decimal> GetInventoryValueAsync()
    {
        return await _db.InventoryBatches
            .Where(b => b.Quantity > 0)
            .SumAsync(b => b.Quantity * b.CostPrice);
    }

    /// <summary>
    /// Критические остатки товаров (меньше минимума)
    /// </summary>
    private async Task<List<StockAlertDto>> GetCriticalStockAlertsAsync()
    {
        return await _db.Products
            .Where(p => p.CurrentStock <= p.MinimumStock && p.CurrentStock > 0)
            .Select(p => new StockAlertDto
            {
                ProductId = p.Id,
                ProductName = p.Name,
                CurrentStock = p.CurrentStock,
                MinimumStock = p.MinimumStock,
                WarehouseType = p.WarehouseType
            })
            .OrderBy(p => p.CurrentStock)
            .Take(10)
            .ToListAsync();
    }

    /// <summary>
    /// Просроченные долги клиентов
    /// </summary>
    private async Task<List<OverdueDebtDto>> GetOverdueDebtsAsync()
    {
        var now = DateTime.UtcNow;
        
        return await _db.Debts
            .Where(d => d.DueDate.HasValue && d.DueDate < now)
            .Join(_db.Sales, d => d.SaleId, s => s.Id, (d, s) => new { Debt = d, Sale = s })
            .Select(x => new OverdueDebtDto
            {
                DebtId = x.Debt.Id,
                ClientName = x.Sale.ClientName,
                Amount = x.Debt.Amount,
                DueDate = x.Debt.DueDate!.Value,
                DaysOverdue = (int)(now - x.Debt.DueDate!.Value).TotalDays
            })
            .OrderByDescending(d => d.DaysOverdue)
            .Take(10)
            .ToListAsync();
    }

    /// <summary>
    /// Получить P&L отчет (прибыли и убытки) за период
    /// </summary>
    public async Task<ProfitLossReportDto> GetProfitLossReportAsync(DateTime from, DateTime to)
    {
        // Выручка
        var revenue = await _db.Sales
            .Where(s => s.CreatedAt >= from && s.CreatedAt < to)
            .SumAsync(s => s.Total);

        // Себестоимость
        var cogs = await _db.SaleItems
            .Where(si => si.Sale!.CreatedAt >= from && si.Sale.CreatedAt < to)
            .SumAsync(si => si.Quantity * si.CostPrice);

        // Валовая прибыль
        var grossProfit = revenue - cogs;
        var grossMargin = revenue > 0 ? (grossProfit / revenue) * 100 : 0;

        // Операционные расходы по типам
        var expensesByType = await _expensesService.GetExpensesByTypeAsync(from, to);
        var totalExpenses = expensesByType.Values.Sum();

        // Операционная прибыль
        var operatingProfit = grossProfit - totalExpenses;

        // Чистая прибыль (пока без налогов, можно добавить позже)
        var netProfit = operatingProfit;
        var netMargin = revenue > 0 ? (netProfit / revenue) * 100 : 0;

        return new ProfitLossReportDto
        {
            Period = $"{from:yyyy-MM-dd} - {to:yyyy-MM-dd}",
            Revenue = revenue,
            COGS = cogs,
            GrossProfit = grossProfit,
            GrossMargin = grossMargin,
            OperatingExpenses = totalExpenses,
            ExpensesByType = expensesByType,
            OperatingProfit = operatingProfit,
            NetProfit = netProfit,
            NetMargin = netMargin
        };
    }

    /// <summary>
    /// Получить Cash Flow за период
    /// </summary>
    public async Task<CashFlowReportDto> GetCashFlowReportAsync(DateTime from, DateTime to)
    {
        // Денежные поступления
        var salesRevenue = await _db.Sales
            .Where(s => s.CreatedAt >= from && s.CreatedAt < to && s.PaymentType != PaymentType.Credit)
            .SumAsync(s => s.Total);

        var debtPayments = await _db.DebtPayments
            .Where(p => p.PaymentDate >= from && p.PaymentDate < to)
            .SumAsync(p => p.Amount);

        var totalInflow = salesRevenue + debtPayments;

        // Денежные выплаты
        var purchasePayments = await _db.Purchases
            .Where(p => p.PaidAt >= from && p.PaidAt < to)
            .SumAsync(p => p.TotalAmount);

        var operatingExpenses = await _expensesService.GetTotalExpensesAsync(from, to);

        var totalOutflow = purchasePayments + operatingExpenses;

        // Чистый денежный поток
        var netCashFlow = totalInflow - totalOutflow;

        return new CashFlowReportDto
        {
            Period = $"{from:yyyy-MM-dd} - {to:yyyy-MM-dd}",
            Inflow = new CashFlowInflowDto
            {
                SalesRevenue = salesRevenue,
                DebtPayments = debtPayments,
                Total = totalInflow
            },
            Outflow = new CashFlowOutflowDto
            {
                PurchasePayments = purchasePayments,
                OperatingExpenses = operatingExpenses,
                Total = totalOutflow
            },
            NetCashFlow = netCashFlow
        };
    }
}

// ===== DTOs =====

public class OwnerDashboardDto
{
    // Финансы за сегодня
    public decimal TodayRevenue { get; set; }
    public decimal TodayProfit { get; set; }
    public int TodaySalesCount { get; set; }
    public decimal TodayAverageCheck { get; set; }

    // Кассы
    public Dictionary<string, decimal> CashboxBalances { get; set; } = new();
    public decimal TotalCash { get; set; }

    // Долги
    public decimal ClientDebts { get; set; }
    public decimal SupplierDebts { get; set; }

    // Склад
    public decimal InventoryValue { get; set; }

    // Топ продукты
    public List<TopProductDto> Top5ProductsToday { get; set; } = new();

    // Алерты
    public List<StockAlertDto> CriticalStockAlerts { get; set; } = new();
    public List<OverdueDebtDto> OverdueDebts { get; set; } = new();

    public DateTime GeneratedAt { get; set; }
}

public class TopProductDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal TotalRevenue { get; set; }
    public int TotalQuantity { get; set; }
}

public class StockAlertDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int MinimumStock { get; set; }
    public string WarehouseType { get; set; } = string.Empty;
}

public class OverdueDebtDto
{
    public int DebtId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public int DaysOverdue { get; set; }
}

public class ProfitLossReportDto
{
    public string Period { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal COGS { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossMargin { get; set; }
    public decimal OperatingExpenses { get; set; }
    public Dictionary<ExpenseType, decimal> ExpensesByType { get; set; } = new();
    public decimal OperatingProfit { get; set; }
    public decimal NetProfit { get; set; }
    public decimal NetMargin { get; set; }
}

public class CashFlowReportDto
{
    public string Period { get; set; } = string.Empty;
    public CashFlowInflowDto Inflow { get; set; } = new();
    public CashFlowOutflowDto Outflow { get; set; } = new();
    public decimal NetCashFlow { get; set; }
}

public class CashFlowInflowDto
{
    public decimal SalesRevenue { get; set; }
    public decimal DebtPayments { get; set; }
    public decimal Total { get; set; }
}

public class CashFlowOutflowDto
{
    public decimal PurchasePayments { get; set; }
    public decimal OperatingExpenses { get; set; }
    public decimal Total { get; set; }
}
