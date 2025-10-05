namespace ProjectApp.Api.Models;

public class ReturnItemRestock
{
    public int Id { get; set; }
    public int ReturnItemId { get; set; }
    public int SaleItemId { get; set; }
    public int BatchId { get; set; }
    public decimal Qty { get; set; }
}
