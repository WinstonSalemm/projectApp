namespace ProjectApp.Core.Costing.Models;

public sealed class CostingConfig
{
    public decimal RubToUzs { get; init; } = 0m;
    public decimal UsdToUzs { get; init; } = 0m;
    public decimal CustomsFixedUzs { get; init; } = 0m;
    public decimal LoadingTotalUzs { get; init; } = 0m;
    public decimal LogisticsPct { get; init; } = 0m;
    public decimal WarehousePct { get; init; } = 0m;
    public decimal DeclarationPct { get; init; } = 0m;
    public decimal CertificationPct { get; init; } = 0m;
    public decimal McsPct { get; init; } = 0m;
    public decimal DeviationPct { get; init; } = 0m;
    public decimal TradeMarkupPct { get; init; } = 0m;
    public decimal VatPct { get; init; } = 0m;
    public decimal ProfitTaxPct { get; init; } = 0m;
}
