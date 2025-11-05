namespace ProjectApp.Api.Costing.Dto;

public sealed class CostingConfigDto
{
    public decimal RubToUzs { get; init; }
    public decimal UsdToUzs { get; init; }
    public decimal CustomsFixedUzs { get; init; }
    public decimal LoadingTotalUzs { get; init; }
    public decimal LogisticsPct { get; init; }
    public decimal WarehousePct { get; init; }
    public decimal DeclarationPct { get; init; }
    public decimal CertificationPct { get; init; }
    public decimal McsPct { get; init; }
    public decimal DeviationPct { get; init; }
    public decimal TradeMarkupPct { get; init; }
    public decimal VatPct { get; init; }
    public decimal ProfitTaxPct { get; init; }
}
