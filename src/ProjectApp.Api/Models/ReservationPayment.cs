namespace ProjectApp.Api.Models;

public enum ReservationPaymentMethod
{
    Cash = 0,
    Card = 1,
    Click = 2,
    Other = 9
}

public class ReservationPayment
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public decimal Amount { get; set; }
    public ReservationPaymentMethod Method { get; set; }
    public string? Note { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    public string ReceivedBy { get; set; } = string.Empty; // username from JWT
}
