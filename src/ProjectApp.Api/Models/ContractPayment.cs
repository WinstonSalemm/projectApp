namespace ProjectApp.Api.Models;

/// <summary>
/// Запись об оплате по договору
/// </summary>
public class ContractPayment
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? Note { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.BankTransfer;
}

public enum PaymentMethod
{
    Cash = 0,           // Наличные
    BankTransfer = 1,   // Банковский перевод
    Card = 2,           // Карта
    Click = 3,          // Click
    Payme = 4           // Payme
}
