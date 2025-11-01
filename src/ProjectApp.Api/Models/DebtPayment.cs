namespace ProjectApp.Api.Models;

public class DebtPayment
{
    public int Id { get; set; }
    public int DebtId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }
    public string? Method { get; set; }
    public string? Comment { get; set; }
    public int? ManagerId { get; set; }
    public string? CreatedBy { get; set; }
}
