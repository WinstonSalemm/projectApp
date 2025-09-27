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
}
