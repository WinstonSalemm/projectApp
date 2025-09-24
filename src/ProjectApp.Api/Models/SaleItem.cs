namespace ProjectApp.Api.Models;

public class SaleItem
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public int ProductId { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
}
