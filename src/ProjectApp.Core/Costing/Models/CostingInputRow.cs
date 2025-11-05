namespace ProjectApp.Core.Costing.Models;

public sealed class CostingInputRow
{
    public string SkuOrName { get; init; } = "";
    public decimal Quantity { get; init; }
    public decimal? PriceRub { get; init; }
    public decimal? PriceUsd { get; init; }
    public decimal? PriceUzs { get; init; }
    public decimal? LineTotalUzsOverride { get; init; }
}
