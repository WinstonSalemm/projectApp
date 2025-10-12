using ProjectApp.Api.Modules.Finance.Models;
using ProjectApp.Api.Modules.Finance.Ratios;

namespace ProjectApp.Api.Modules.Finance;

public record FinanceKpi(
    decimal Roi,
    decimal Roa,
    decimal Roe,
    decimal InventoryTurnover,
    decimal AverageCheck,
    decimal Conversion,
    decimal DebtToRevenue
);

public class FinanceMetricsCalculator(FinanceSettings settings)
{
    private readonly FinanceSettings _settings = settings;

    public (decimal grossProfit, decimal marginPercent, decimal netProfit) ComputeProfit(decimal revenue, decimal cogs, decimal expenses, decimal taxesPaid)
    {
        var gross = revenue - cogs;
        var margin = revenue == 0 ? 0 : SafeRound(gross / (revenue == 0 ? 1 : revenue) * 100m);
        var net = gross - expenses - taxesPaid;
        return (SafeRound(gross), margin, SafeRound(net));
    }

    public FinanceKpi ComputeKpi(
        decimal netProfit,
        decimal revenue,
        decimal cogs,
        int salesCount,
        int uniqueClients,
        decimal averageInventoryQty,
        decimal totalDebts,
        decimal? totalInvestments,
        decimal? totalAssets,
        decimal? equity)
    {
        var invTurnover = averageInventoryQty == 0 ? 0 : SafeRound(cogs / (averageInventoryQty == 0 ? 1 : averageInventoryQty));
        var avgCheck = salesCount == 0 ? 0 : SafeRound(revenue / salesCount);
        var conversion = uniqueClients == 0 ? 0 : SafeRound((decimal)salesCount / uniqueClients);
        var debtToRevenue = revenue == 0 ? 0 : SafeRound(totalDebts / (revenue == 0 ? 1 : revenue));
        var roi = (totalInvestments ?? 0) == 0 ? 0 : SafeRound(netProfit / ((totalInvestments ?? 1)) * 100m);
        var roa = (totalAssets ?? 0) == 0 ? 0 : SafeRound(netProfit / ((totalAssets ?? 1)) * 100m);
        var roe = (equity ?? 0) == 0 ? 0 : SafeRound(netProfit / ((equity ?? 1)) * 100m);
        return new FinanceKpi(roi, roa, roe, invTurnover, avgCheck, conversion, debtToRevenue);
    }

    private static decimal SafeRound(decimal v) => decimal.Round(v, 2, MidpointRounding.AwayFromZero);

    public LiquidityRatiosDto ComputeLiquidityRatios(
        decimal cash,
        decimal accountsReceivable,
        decimal inventory,
        decimal currentLiabilities,
        decimal? totalAssetsOverride,
        decimal? equityOverride)
    {
        var currentAssets = cash + accountsReceivable + inventory;
        var totalAssets = totalAssetsOverride ?? currentAssets;
        var equity = equityOverride ?? (_settings.Equity ?? 0m);
        decimal Div(decimal a, decimal b) => b == 0 ? 0 : SafeRound(a / b);
        return new LiquidityRatiosDto
        {
            CurrentAssets = currentAssets,
            CurrentLiabilities = currentLiabilities,
            Cash = cash,
            AccountsReceivable = accountsReceivable,
            Inventory = inventory,
            CurrentRatio = Div(currentAssets, currentLiabilities),
            QuickRatio = Div(currentAssets - inventory, currentLiabilities),
            DebtRatio = Div(currentLiabilities, totalAssets == 0 ? 1 : totalAssets),
            DebtToEquity = Div(currentLiabilities, equity == 0 ? 1 : equity),
            WorkingCapital = currentAssets - currentLiabilities
        };
    }
}
