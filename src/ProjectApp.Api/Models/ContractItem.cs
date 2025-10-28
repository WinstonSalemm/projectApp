namespace ProjectApp.Api.Models;

public class ContractItem
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = default!;
    
    /// <summary>
    /// ID товара из каталога (nullable - для "будущих" товаров которых ещё нет в НД)
    /// </summary>
    public int? ProductId { get; set; }
    
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty; // snapshot name
    
    /// <summary>
    /// Описание товара (для случая когда ProductId = null и товар ещё не пришёл в НД)
    /// </summary>
    public string? Description { get; set; }
    
    public string Unit { get; set; } = "шт";
    public decimal Qty { get; set; }          // Количество по договору
    public decimal DeliveredQty { get; set; } // Отгружено
    public decimal UnitPrice { get; set; }
    
    /// <summary>
    /// Статус позиции (зарезервирован/отгружен/отменён)
    /// </summary>
    public ContractItemStatus Status { get; set; } = ContractItemStatus.Reserved;
    
    // Computed properties
    public bool IsDelivered => DeliveredQty >= Qty;
    public decimal TotalPrice => Qty * UnitPrice;
}
