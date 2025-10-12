using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;

namespace ProjectApp.Api.Modules.Finance.CashFlow;

public class FinanceCashFlowCalculator(AppDbContext db)
{
    private readonly AppDbContext _db = db;

    public async Task<CashFlowDto> ComputeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        // Operating cash in: fallback to revenue if no explicit payments table exists
        var revenue = await (from s in _db.Sales.AsNoTracking()
                             where s.CreatedAt >= fromUtc && s.CreatedAt < toUtc
                             join i in _db.SaleItems.AsNoTracking() on s.Id equals i.SaleId
                             select (decimal?)(i.UnitPrice * i.Qty)).SumAsync(ct) ?? 0m;

        // Operating out: expenses + taxes paid
        var expenses = await _db.Expenses.AsNoTracking().Where(e => e.Date >= fromUtc && e.Date < toUtc).SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var taxesPaid = await _db.TaxPayments.AsNoTracking().Where(t => t.PaidAt >= fromUtc && t.PaidAt < toUtc).SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;

        // Investing: treat new ND40 batches as purchases (approximation)
        var investingOut = await _db.Batches.AsNoTracking()
            .Where(b => b.CreatedAt >= fromUtc && b.CreatedAt < toUtc)
            .SumAsync(b => (decimal?)(b.UnitCost * b.Qty), ct) ?? 0m;
        var investingIn = 0m;

        // Financing: absent explicit data, default to 0 for now
        var financingIn = 0m;
        var financingOut = 0m;

        var ocf = revenue - (expenses + taxesPaid);
        var icf = investingIn - investingOut;
        var fcf = financingIn - financingOut;
        var net = ocf + icf + fcf;

        return new CashFlowDto
        {
            OperatingIn = revenue,
            OperatingOut = expenses + taxesPaid,
            OCF = ocf,
            InvestingIn = investingIn,
            InvestingOut = investingOut,
            ICF = icf,
            FinancingIn = financingIn,
            FinancingOut = financingOut,
            FCF = fcf,
            NetCashFlow = net
        };
    }
}
