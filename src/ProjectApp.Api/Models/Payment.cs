namespace ProjectApp.Api.Models;

public class Payment
{
    public int Id { get; set; }
    public int? ContractId { get; set; }
    public int? SaleId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "bank"; // bank/cash/card
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
}

public class Prepayment
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public decimal Amount { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
}
