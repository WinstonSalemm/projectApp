namespace ProjectApp.Api.Modules.Finance.Dtos;

public record FinanceSummaryDto(
    decimal Revenue,
    decimal Cogs,
    decimal GrossProfit,
    decimal NetProfit,
    decimal MarginPercent,
    decimal Expenses,
    decimal TaxesPaid,
    int SalesCount,
    int UniqueClients,
    decimal AverageInventory,
    object? Series,
    IEnumerable<GroupPoint>? Groups
);
