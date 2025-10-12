using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Modules.Finance.Models;
using Microsoft.Extensions.Options;

namespace ProjectApp.Api.Modules.Finance.Ratios;

public class LiquidityService(AppDbContext db, IOptions<FinanceSettings> settings)
{
    private readonly AppDbContext _db = db;
    private readonly FinanceSettings _settings = settings.Value;

    public async Task<LiquidityRatiosDto> ComputeAsync(CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;
        // Cash approximation: cumulative revenue - expenses - taxes - purchases (batches cost) - debt repayments
        var totalRevenue = await (from s in _db.Sales.AsNoTracking()
                                  join i in _db.SaleItems.AsNoTracking() on s.Id equals i.SaleId
                                  select (decimal?)(i.UnitPrice * i.Qty)).SumAsync(ct) ?? 0m;
        var totalExpenses = await _db.Expenses.AsNoTracking().SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var totalTaxesPaid = await _db.TaxPayments.AsNoTracking().SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;
        var totalPurchases = await _db.Batches.AsNoTracking().SumAsync(b => (decimal?)(b.UnitCost * b.Qty), ct) ?? 0m;
        var totalDebtRepay = await _db.DebtPayments.AsNoTracking().SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var cash = totalRevenue - totalExpenses - totalTaxesPaid - totalPurchases - totalDebtRepay;
        if (cash < 0) cash = 0; // don't show negative cash if approximation overshoots

        // Accounts receivable: Debts outstanding
        var ar = await (from d in _db.Debts.AsNoTracking()
                        let paid = _db.DebtPayments.Where(p => p.DebtId == d.Id).Sum(p => (decimal?)p.Amount) ?? 0m
                        select (decimal?)(d.Amount - paid)).SumAsync(ct) ?? 0m;

        // Inventory valuation: sum remaining batches cost
        var inventory = await _db.Batches.AsNoTracking().SumAsync(b => (decimal?)(b.Qty * b.UnitCost), ct) ?? 0m;

        // Current liabilities approximation: taxes payable (accrued - paid) + short-term debts (DueDate <= 1y)
        // Accrued VAT and ProfitTax (rough)
        var accruedVat = totalRevenue * (_settings.TaxRates.Vat / 100m);
        var accruedProfitTax = 0m; // need net profit; approximate from revenue - purchases - expenses - taxesPaid
        var grossApprox = totalRevenue - totalPurchases; // rough
        var netApprox = grossApprox - totalExpenses - totalTaxesPaid;
        if (netApprox > 0) accruedProfitTax = netApprox * (_settings.TaxRates.ProfitTax / 100m);
        var taxesPayable = accruedVat + accruedProfitTax - totalTaxesPaid;
        if (taxesPayable < 0) taxesPayable = 0;

        var shortTermDebts = await _db.Debts.AsNoTracking()
            .Where(d => d.DueDate <= nowUtc.AddDays(365))
            .SumAsync(d => (decimal?)d.Amount, ct) ?? 0m;

        var currentLiabilities = taxesPayable + shortTermDebts;
        var currentAssets = cash + ar + inventory;
        var totalAssets = _settings.TotalAssets ?? (currentAssets);
        var equity = _settings.Equity ?? 0m;
        var totalLiabilities = currentLiabilities; // approximation without long-term data

        decimal SafeDiv(decimal a, decimal b) => b == 0 ? 0 : decimal.Round(a / b, 2, MidpointRounding.AwayFromZero);

        return new LiquidityRatiosDto
        {
            CurrentAssets = currentAssets,
            CurrentLiabilities = currentLiabilities,
            Cash = cash,
            AccountsReceivable = ar,
            Inventory = inventory,
            CurrentRatio = SafeDiv(currentAssets, currentLiabilities),
            QuickRatio = SafeDiv(currentAssets - inventory, currentLiabilities),
            DebtRatio = SafeDiv(totalLiabilities, totalAssets == 0 ? 1 : totalAssets),
            DebtToEquity = SafeDiv(totalLiabilities, equity == 0 ? 1 : equity),
            WorkingCapital = currentAssets - currentLiabilities
        };
    }
}
