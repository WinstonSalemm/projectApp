using Microsoft.Extensions.Options;
using ProjectApp.Api.Modules.Finance.Models;

namespace ProjectApp.Api.Modules.Finance.Alerts;

public sealed class FinanceAlert
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal? Value { get; set; }
    public decimal? Threshold { get; set; }
}

public class FinanceAlertService
{
    public FinanceSettings Settings { get; }
    private readonly FinanceService _finance;

    public FinanceAlertService(IOptions<FinanceSettings> settings, FinanceService finance)
    {
        Settings = settings.Value;
        _finance = finance;
    }

    public async Task<IReadOnlyList<FinanceAlert>> EvaluateAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var alerts = new List<FinanceAlert>();
        var kpi = await _finance.GetKpiAsync(from, to, ct);

        if (Settings.Alerts.NetProfitBelow > 0 && kpi is not null)
        {
            // For period net profit we need summary
            var summary = await _finance.GetSummaryAsync(from, to, null, null, ct);
            if (summary.NetProfit < Settings.Alerts.NetProfitBelow)
            {
                alerts.Add(new FinanceAlert
                {
                    Code = "NET_PROFIT_LOW",
                    Message = $"Чистая прибыль ниже порога: {summary.NetProfit:N0} < {Settings.Alerts.NetProfitBelow:N0}",
                    Value = summary.NetProfit,
                    Threshold = Settings.Alerts.NetProfitBelow
                });
            }
        }

        if (Settings.Alerts.DebtToRevenueAbove > 0 && kpi.DebtToRevenue > Settings.Alerts.DebtToRevenueAbove)
        {
            alerts.Add(new FinanceAlert
            {
                Code = "DEBT_TO_REVENUE_HIGH",
                Message = $"Отношение долг/выручка выше порога: {kpi.DebtToRevenue:P2} > {Settings.Alerts.DebtToRevenueAbove:P2}",
                Value = kpi.DebtToRevenue,
                Threshold = Settings.Alerts.DebtToRevenueAbove
            });
        }

        // Expenses growth MoM: compare last two months summaries
        if (Settings.Alerts.ExpensesGrowthAbovePercent > 0)
        {
            var now = DateTime.UtcNow.Date;
            var fromPrev = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-2);
            var toPrev = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
            var fromCurr = toPrev;
            var toCurr = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var sPrev = await _finance.GetSummaryAsync(fromPrev, toPrev, null, null, ct);
            var sCurr = await _finance.GetSummaryAsync(fromCurr, toCurr, null, null, ct);
            if (sPrev.Expenses > 0)
            {
                var growth = (sCurr.Expenses - sPrev.Expenses) / sPrev.Expenses * 100m;
                if (growth > Settings.Alerts.ExpensesGrowthAbovePercent)
                {
                    alerts.Add(new FinanceAlert
                    {
                        Code = "EXPENSES_GROWTH_HIGH",
                        Message = $"Рост расходов MoM {growth:N2}% > {Settings.Alerts.ExpensesGrowthAbovePercent:N2}%",
                        Value = growth,
                        Threshold = Settings.Alerts.ExpensesGrowthAbovePercent
                    });
                }
            }
        }

        return alerts;
    }
}
