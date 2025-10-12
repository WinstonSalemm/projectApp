namespace ProjectApp.Api.Modules.Finance.Models;

public class FinanceSettings
{
    public int TimeZoneOffsetMinutes { get; set; } = 300; // default UTC+5
    public TaxRates TaxRates { get; set; } = new();
    public AlertsThresholds Alerts { get; set; } = new();

    // Capital base for ROI/ROA/ROE and liquidity ratios
    public decimal? TotalInvestments { get; set; }
    public decimal? TotalAssets { get; set; }
    public decimal? Equity { get; set; }

    // Thresholds for ABC/XYZ
    public (double A, double B) AbcThresholds { get; set; } = (0.80, 0.95);
    public (double X, double Y) XyzThresholds { get; set; } = (0.10, 0.25);
}

public class TaxRates
{
    public decimal Vat { get; set; } = 12m;
    public decimal ProfitTax { get; set; } = 12m;
    public decimal PayrollTax { get; set; } = 0m;
    public decimal SocialTax { get; set; } = 0m;
}

public class AlertsThresholds
{
    public decimal NetProfitBelow { get; set; } = 0m;
    public decimal ExpensesGrowthAbovePercent { get; set; } = 50m;
    public decimal DebtToRevenueAbove { get; set; } = 1.0m; // 100%
}
