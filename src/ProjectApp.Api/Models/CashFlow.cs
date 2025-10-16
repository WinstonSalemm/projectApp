namespace ProjectApp.Api.Models;

public enum CashFlowType
{
    Income,     // Приход
    Expense     // Расход
}

public class CashFlow
{
    public int Id { get; set; }
    public CashFlowType Type { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
