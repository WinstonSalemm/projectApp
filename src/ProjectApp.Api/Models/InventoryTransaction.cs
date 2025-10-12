using ProjectApp.Api.Models;

namespace ProjectApp.Api.Models;

public class InventoryTransaction
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public StockRegister Register { get; set; }
    public InventoryTransactionType Type { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
    public int? BatchId { get; set; }
    public int? SaleId { get; set; }
    public int? ReturnId { get; set; }
    public int? ReservationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? Note { get; set; }
}
