namespace ProjectApp.Api.Models;

public class FinancialPlan
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal PlannedRevenue { get; set; }
    public decimal PlannedExpenses { get; set; }
    public decimal PlannedProfit => PlannedRevenue - PlannedExpenses;
    public string? Notes { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
