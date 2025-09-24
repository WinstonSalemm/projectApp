using System.Collections.ObjectModel;

namespace ProjectApp.Api.Models;

public class Sale
{
    public int Id { get; set; }
    public int? ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public List<SaleItem> Items { get; set; } = new();
    public PaymentType PaymentType { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
}
