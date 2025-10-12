using ProjectApp.Api.Models;

namespace ProjectApp.Api.Models;

public class InventoryConsumption
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public int BatchId { get; set; }
    public StockRegister Register { get; set; }
    public decimal Qty { get; set; }
    public int? SaleItemId { get; set; }
    public int? ReturnItemId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
}
