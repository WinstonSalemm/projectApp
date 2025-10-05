namespace ProjectApp.Api.Models;

public class SaleItemConsumption
{
    public int Id { get; set; }
    public int SaleItemId { get; set; }
    public int BatchId { get; set; }
    public StockRegister RegisterAtSale { get; set; }
    public decimal Qty { get; set; }
}
