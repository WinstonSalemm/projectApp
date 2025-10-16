using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class FinanceController(AppDbContext db) : ControllerBase
{
    // GET /api/finance/dashboard?from=2025-01-01&to=2025-12-31&includeGrey=true&includeBlack=true
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool includeGrey = true,
        [FromQuery] bool includeBlack = true,
        CancellationToken ct = default)
    {
        var dateFrom = from ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var dateTo = to ?? dateFrom.AddMonths(1);

        // Определяем какие категории продаж включать
        var categories = new List<SaleCategory> { SaleCategory.White };
        if (includeGrey) categories.Add(SaleCategory.Grey);
        if (includeBlack) categories.Add(SaleCategory.Black);

        // 1. Выручка и себестоимость
        var sales = await db.Sales
            .AsNoTracking()
            .Where(s => s.CreatedAt >= dateFrom && s.CreatedAt < dateTo && categories.Contains(s.Category))
            .SelectMany(s => s.Items.Select(i => new { i.Qty, i.UnitPrice, i.Cost }))
            .ToListAsync(ct);

        var revenue = sales.Sum(s => s.Qty * s.UnitPrice);
        var cost = sales.Sum(s => s.Qty * s.Cost);
        var grossProfit = revenue - cost;

        // 2. Операционные расходы
        var expenses = await db.Set<Expense>()
            .AsNoTracking()
            .Where(e => e.Date >= dateFrom && e.Date < dateTo)
            .SumAsync(e => e.Amount, ct);

        var netProfit = grossProfit - expenses;

        // 3. Маржинальность и рентабельность
        var grossMargin = revenue > 0 ? (grossProfit / revenue) * 100 : 0;
        var netMargin = revenue > 0 ? (netProfit / revenue) * 100 : 0;

        // 4. Денежный поток
        var cashInflows = await db.Set<CashFlow>()
            .AsNoTracking()
            .Where(cf => cf.Date >= dateFrom && cf.Date < dateTo && cf.Type == CashFlowType.Income)
            .SumAsync(cf => cf.Amount, ct);

        var cashOutflows = await db.Set<CashFlow>()
            .AsNoTracking()
            .Where(cf => cf.Date >= dateFrom && cf.Date < dateTo && cf.Type == CashFlowType.Expense)
            .SumAsync(cf => cf.Amount, ct);

        var operatingCashFlow = cashInflows - cashOutflows;

        // 5. Дебиторская задолженность
        var receivables = await db.Set<Debt>()
            .AsNoTracking()
            .Where(d => d.Status == DebtStatus.Open || d.Status == DebtStatus.Overdue)
            .SumAsync(d => d.Amount, ct);

        // 6. Кредиторская задолженность
        var payables = await db.Set<Liability>()
            .AsNoTracking()
            .Where(l => !l.IsPaid)
            .SumAsync(l => l.Amount, ct);

        // 7. Активы
        var assets = await db.Set<Asset>()
            .AsNoTracking()
            .ToListAsync(ct);

        var totalAssets = assets.Sum(a => a.CurrentValue);

        // 8. ROA, ROE (если есть капитал)
        decimal? roa = totalAssets > 0 ? (netProfit / totalAssets) * 100 : null;

        // 9. Коэффициенты ликвидности
        decimal? currentRatio = payables > 0 ? receivables / payables : null;

        return Ok(new
        {
            period = new { from = dateFrom, to = dateTo },
            revenue,
            cost,
            grossProfit,
            grossMargin,
            expenses,
            netProfit,
            netMargin,
            operatingCashFlow,
            receivables,
            payables,
            totalAssets,
            roa,
            currentRatio
        });
    }

    // POST /api/finance/expenses
    [HttpPost("expenses")]
    public async Task<IActionResult> AddExpense([FromBody] Expense expense, CancellationToken ct)
    {
        expense.CreatedBy = User.Identity?.Name;
        expense.CreatedAt = DateTime.UtcNow;
        db.Set<Expense>().Add(expense);
        await db.SaveChangesAsync(ct);
        return Ok(expense);
    }

    // POST /api/finance/cashflows
    [HttpPost("cashflows")]
    public async Task<IActionResult> AddCashFlow([FromBody] CashFlow cashFlow, CancellationToken ct)
    {
        cashFlow.CreatedBy = User.Identity?.Name;
        cashFlow.CreatedAt = DateTime.UtcNow;
        db.Set<CashFlow>().Add(cashFlow);
        await db.SaveChangesAsync(ct);
        return Ok(cashFlow);
    }

    // POST /api/finance/liabilities
    [HttpPost("liabilities")]
    public async Task<IActionResult> AddLiability([FromBody] Liability liability, CancellationToken ct)
    {
        liability.CreatedBy = User.Identity?.Name;
        liability.CreatedAt = DateTime.UtcNow;
        db.Set<Liability>().Add(liability);
        await db.SaveChangesAsync(ct);
        return Ok(liability);
    }

    // POST /api/finance/assets
    [HttpPost("assets")]
    public async Task<IActionResult> AddAsset([FromBody] Asset asset, CancellationToken ct)
    {
        asset.CreatedBy = User.Identity?.Name;
        asset.CreatedAt = DateTime.UtcNow;
        db.Set<Asset>().Add(asset);
        await db.SaveChangesAsync(ct);
        return Ok(asset);
    }

    // POST /api/finance/plans
    [HttpPost("plans")]
    public async Task<IActionResult> AddPlan([FromBody] FinancialPlan plan, CancellationToken ct)
    {
        plan.CreatedBy = User.Identity?.Name;
        plan.CreatedAt = DateTime.UtcNow;
        db.Set<FinancialPlan>().Add(plan);
        await db.SaveChangesAsync(ct);
        return Ok(plan);
    }
}
