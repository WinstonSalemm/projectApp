namespace ProjectApp.Client.Maui.Models;

public class SaleModel
{
    public int Id { get; set; }
    public int? ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public PaymentType PaymentType { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

