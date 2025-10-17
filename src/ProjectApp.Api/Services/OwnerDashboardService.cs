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

        // Выполняем запросы последовательно для простоты
        var todayRevenue = await GetTodayRevenueAsync(startOfDay, endOfDay);
        var todayProfit = await GetTodayProfitAsync(startOfDay, endOfDay);
        var todaySalesCount = await GetTodaySalesCountAsync(startOfDay, endOfDay);
        var todayAvgCheck = await GetTodayAverageCheckAsync(startOfDay, endOfDay);
        var top5Products = await GetTop5ProductsTodayAsync(startOfDay, endOfDay);

        var cashboxBalances = await _cashboxService.GetTotalBalancesByCurrencyAsync();
        var clientDebts = await GetTotalClientDebtsAsync();
        var supplierDebts = await GetTotalSupplierDebtsAsync();
        var inventoryValue = await GetInventoryValueAsync();
        var criticalStock = await GetCriticalStockAlertsAsync();
        var overdueDebts = await GetOverdueDebtsAsync();

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

        // Себестоимость проданных товаров - через join
        var cogs = await (from sale in _db.Sales
                         where sale.CreatedAt >= from && sale.CreatedAt < to
                         join saleItem in _db.SaleItems on sale.Id equals saleItem.SaleId
                         select saleItem.Qty * saleItem.Cost).SumAsync();

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
        return await (from sale in _db.Sales
                     where sale.CreatedAt >= from && sale.CreatedAt < to
                     join saleItem in _db.SaleItems on sale.Id equals saleItem.SaleId
                     join product in _db.Products on saleItem.ProductId equals product.Id
                     group saleItem by new { saleItem.ProductId, product.Name } into g
                     select new TopProductDto
                     {
                         ProductId = g.Key.ProductId,
                         ProductName = g.Key.Name,
                         TotalRevenue = g.Sum(si => si.UnitPrice * si.Qty),
                         TotalQuantity = (int)g.Sum(si => si.Qty)
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
        // Временно возвращаем 0, т.к. нет таблицы Purchases
        return 0m;
    }

    /// <summary>
    /// Стоимость товара на складе (по себестоимости)
    /// </summary>
    private async Task<decimal> GetInventoryValueAsync()
    {
        return await _db.Batches
            .Where(b => b.Qty > 0)
            .SumAsync(b => b.Qty * b.UnitCost);
    }

    /// <summary>
    /// Критические остатки товаров (меньше минимума)
    /// </summary>
    private async Task<List<StockAlertDto>> GetCriticalStockAlertsAsync()
    {
        // Получаем остатки из Stocks и группируем по продукту
        var criticalStocks = await (from stock in _db.Stocks
                                   group stock by stock.ProductId into g
                                   let totalQty = g.Sum(s => s.Qty)
                                   where totalQty <= 10 && totalQty > 0 // Минимальный порог 10
                                   join product in _db.Products on g.Key equals product.Id
                                   select new StockAlertDto
                                   {
                                       ProductId = product.Id,
                                       ProductName = product.Name,
                                       CurrentStock = (int)totalQty,
                                       MinimumStock = 10,
                                       WarehouseType = "Mixed"
                                   })
            .OrderBy(p => p.CurrentStock)
            .Take(10)
            .ToListAsync();
        
        return criticalStocks;
    }

    /// <summary>
    /// Просроченные долги клиентов
    /// </summary>
    private async Task<List<OverdueDebtDto>> GetOverdueDebtsAsync()
    {
        var now = DateTime.UtcNow;
        
        return await _db.Debts
            .Where(d => d.DueDate < now && d.Status == DebtStatus.Open)
            .Join(_db.Sales, d => d.SaleId, s => s.Id, (d, s) => new { Debt = d, Sale = s })
            .Select(x => new OverdueDebtDto
            {
                DebtId = x.Debt.Id,
                ClientName = x.Sale.ClientName,
                Amount = x.Debt.Amount,
                DueDate = x.Debt.DueDate,
                DaysOverdue = (int)(now - x.Debt.DueDate).TotalDays
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

        // Себестоимость через join
        var cogs = await (from sale in _db.Sales
                         where sale.CreatedAt >= from && sale.CreatedAt < to
                         join saleItem in _db.SaleItems on sale.Id equals saleItem.SaleId
                         select saleItem.Qty * saleItem.Cost).SumAsync();

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
            .Where(s => s.CreatedAt >= from && s.CreatedAt < to)
            .SumAsync(s => s.Total);

        var debtPayments = await _db.DebtPayments
            .Where(p => p.PaidAt >= from && p.PaidAt < to)
            .SumAsync(p => p.Amount);

        var totalInflow = salesRevenue + debtPayments;

        // Денежные выплаты
        var purchasePayments = 0m; // Временно, т.к. нет таблицы Purchases

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
