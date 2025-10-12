using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Modules.Finance.Models;

namespace ProjectApp.Api.Modules.Finance.Taxes;

public class TaxCalculatorService(AppDbContext db, FinanceSettings settings)
{
    private readonly AppDbContext _db = db;
    private readonly FinanceSettings _settings = settings;

    public async Task<TaxesBreakdownDto> ComputeBreakdownAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var sales = await (from s in _db.Sales.AsNoTracking()
                           where s.CreatedAt >= fromUtc && s.CreatedAt < toUtc
                           join i in _db.SaleItems.AsNoTracking() on s.Id equals i.SaleId
                           select new { Rev = i.UnitPrice * i.Qty, Cogs = i.Cost * i.Qty }).ToListAsync(ct);
        var revenue = sales.Sum(x => x.Rev);
        var cogs = sales.Sum(x => x.Cogs);
        var expenses = await _db.Expenses.AsNoTracking().Where(e => e.Date >= fromUtc && e.Date < toUtc).SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var taxesPaid = await _db.TaxPayments.AsNoTracking().Where(t => t.PaidAt >= fromUtc && t.PaidAt < toUtc).SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;

        var gross = revenue - cogs;
        var net = gross - expenses; // без учёта начисленных налогов

        var vatAccrued = revenue * (_settings.TaxRates.Vat / 100m);
        var profitTaxAccrued = net > 0 ? net * (_settings.TaxRates.ProfitTax / 100m) : 0m;
        var payrollBase = await _db.Expenses.AsNoTracking().Where(e => e.Date >= fromUtc && e.Date < toUtc && e.Category == "Payroll").SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var payrollTaxAccrued = payrollBase * (_settings.TaxRates.PayrollTax / 100m);
        var socialTaxAccrued = payrollBase * (_settings.TaxRates.SocialTax / 100m);

        return new TaxesBreakdownDto
        {
            VatAccrued = decimal.Round(vatAccrued, 2),
            ProfitTaxAccrued = decimal.Round(profitTaxAccrued, 2),
            PayrollTaxAccrued = decimal.Round(payrollTaxAccrued, 2),
            SocialTaxAccrued = decimal.Round(socialTaxAccrued, 2),
            TaxesPaid = taxesPaid
        };
    }
}
