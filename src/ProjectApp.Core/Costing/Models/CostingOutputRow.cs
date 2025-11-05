namespace ProjectApp.Core.Costing.Models;

public sealed class CostingOutputRow
{
    public string SkuOrName { get; init; } = "";
    public decimal Quantity { get; init; }

    public decimal BasePriceUzs { get; init; }
    public decimal LineBaseTotalUzs { get; init; }

    public decimal CustomsUzsPerUnit { get; init; }
    public decimal LoadingUzsPerUnit { get; init; }
    public decimal LogisticsUzsPerUnit { get; init; }
    public decimal WarehouseUzsPerUnit { get; init; }
    public decimal DeclarationUzsPerUnit { get; init; }
    public decimal CertificationUzsPerUnit { get; init; }
    public decimal McsUzsPerUnit { get; init; }
    public decimal DeviationUzsPerUnit { get; init; }

    public decimal CostPerUnitUzs { get; init; }

    public decimal TradePriceUzs { get; init; }
    public decimal VatUzs { get; init; }
    public decimal PriceWithVatUzs { get; init; }
    public decimal ProfitPerUnitUzs { get; init; }
    public decimal ProfitTaxUzs { get; init; }
    public decimal NetProfitUzs { get; init; }

    public string[] Warnings { get; init; } = System.Array.Empty<string>();
}
