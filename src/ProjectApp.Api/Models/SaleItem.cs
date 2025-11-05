using System.Collections.Generic;

namespace ProjectApp.Api.Models;

public class SaleItem
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public int ProductId { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
    // Average unit cost (COGS) for this sale item, computed from FIFO batches at the time of sale
    public decimal Cost { get; set; }
    
    // Snapshot of product info at time of sale (for returns and history)
    public string? Sku { get; set; }
    public string? ProductName { get; set; }

    public ICollection<SaleItemConsumption> Consumptions { get; set; } = new List<SaleItemConsumption>();
}
