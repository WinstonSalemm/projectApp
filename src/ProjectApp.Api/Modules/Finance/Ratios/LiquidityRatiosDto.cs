namespace ProjectApp.Api.Modules.Finance.Ratios;

public sealed class LiquidityRatiosDto
{
    public decimal CurrentAssets { get; set; }
    public decimal CurrentLiabilities { get; set; }
    public decimal Cash { get; set; }
    public decimal AccountsReceivable { get; set; }
    public decimal Inventory { get; set; }
    public decimal CurrentRatio { get; set; }
    public decimal QuickRatio { get; set; }
    public decimal DebtRatio { get; set; }
    public decimal DebtToEquity { get; set; }
    public decimal WorkingCapital { get; set; }
}
