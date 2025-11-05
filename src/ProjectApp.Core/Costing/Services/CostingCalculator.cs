using ProjectApp.Core.Costing.Models;

namespace ProjectApp.Core.Costing.Services;

public sealed class CostingCalculator
{
    private static decimal R2(decimal v) => decimal.Round(v, 2, MidpointRounding.AwayFromZero);

    public sealed record Result(
        IReadOnlyList<CostingOutputRow> Rows,
        decimal TotalQty,
        decimal TotalBaseSumUzs,
        string[] Warnings
    );

    public Result Calculate(IReadOnlyList<CostingInputRow> items, CostingConfig cfg)
    {
        var warnings = new List<string>();
        var qtySum = items.Sum(i => i.Quantity);
        if (qtySum <= 0) warnings.Add("Total quantity = 0; fixed overheads per unit set to 0.");

        var rowsBase = items.Select(i =>
        {
            decimal basePrice = 0m;
            if (i.PriceUzs.HasValue && i.PriceUzs.Value > 0)
                basePrice = i.PriceUzs.Value;
            else if (i.PriceUsd.HasValue && i.PriceUsd.Value > 0)
                basePrice = i.PriceUsd.Value * cfg.UsdToUzs;
            else if (i.PriceRub.HasValue && i.PriceRub.Value > 0)
                basePrice = i.PriceRub.Value * cfg.RubToUzs;

            var lineBase = i.LineTotalUzsOverride ?? (basePrice * i.Quantity);
            return (i, basePrice, lineBase);
        }).ToList();

        var customsPerUnit = qtySum > 0 ? cfg.CustomsFixedUzs / qtySum : 0m;
        var loadingPerUnit = qtySum > 0 ? cfg.LoadingTotalUzs / qtySum : 0m;

        var outRows = new List<CostingOutputRow>(rowsBase.Count);
        foreach (var (i, basePrice, lineBase) in rowsBase)
        {
            var logistics    = basePrice * cfg.LogisticsPct;
            var warehouse    = basePrice * cfg.WarehousePct;
            var declaration  = basePrice * cfg.DeclarationPct;
            var cert         = basePrice * cfg.CertificationPct;
            var mcs          = basePrice * cfg.McsPct;
            var dev          = basePrice * cfg.DeviationPct;

            var costPerUnit =
                basePrice + customsPerUnit + loadingPerUnit +
                logistics + warehouse + declaration + cert + mcs + dev;

            var tradePrice   = costPerUnit * (1 + cfg.TradeMarkupPct);
            var vat          = tradePrice * cfg.VatPct;
            var priceWithVat = tradePrice + vat;

            var profit       = tradePrice - costPerUnit;
            var profitTax    = profit * cfg.ProfitTaxPct;
            var netProfit    = profit - profitTax;

            outRows.Add(new CostingOutputRow
            {
                SkuOrName = i.SkuOrName,
                Quantity = R2(i.Quantity),

                BasePriceUzs = R2(basePrice),
                LineBaseTotalUzs = R2(lineBase),

                CustomsUzsPerUnit = R2(customsPerUnit),
                LoadingUzsPerUnit = R2(loadingPerUnit),
                LogisticsUzsPerUnit = R2(logistics),
                WarehouseUzsPerUnit = R2(warehouse),
                DeclarationUzsPerUnit = R2(declaration),
                CertificationUzsPerUnit = R2(cert),
                McsUzsPerUnit = R2(mcs),
                DeviationUzsPerUnit = R2(dev),

                CostPerUnitUzs = R2(costPerUnit),

                TradePriceUzs = R2(tradePrice),
                VatUzs = R2(vat),
                PriceWithVatUzs = R2(priceWithVat),
                ProfitPerUnitUzs = R2(profit),
                ProfitTaxUzs = R2(profitTax),
                NetProfitUzs = R2(netProfit),

                Warnings = Array.Empty<string>()
            });
        }

        return new Result(
            Rows: outRows,
            TotalQty: R2(qtySum),
            TotalBaseSumUzs: R2(rowsBase.Sum(x => x.lineBase)),
            Warnings: warnings.ToArray()
        );
    }
}
