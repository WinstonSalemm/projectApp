namespace ProjectApp.Api.Models;

public enum DebtStatus
{
    Open = 0,
    Paid = 1,
    Overdue = 2,
    Canceled = 3
}

public class Debt
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public int SaleId { get; set; }
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public DebtStatus Status { get; set; }
}
