namespace ProjectApp.Api.Models;

public class ContractItem
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public int? ProductId { get; set; } // optional link to catalog
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty; // snapshot name
    public string Unit { get; set; } = "шт";
    public decimal Qty { get; set; }          // Количество по договору
    public decimal DeliveredQty { get; set; } // Отгружено
    public decimal UnitPrice { get; set; }
    
    // Статус позиции
    public bool IsDelivered => DeliveredQty >= Qty;
}
