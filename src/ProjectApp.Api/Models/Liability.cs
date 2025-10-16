namespace ProjectApp.Api.Models;

public enum LiabilityType
{
    Supplier,   // Долг поставщику
    Loan,       // Кредит/займ
    Tax,        // Налоги
    Other       // Прочее
}

public class Liability
{
    public int Id { get; set; }
    public LiabilityType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
