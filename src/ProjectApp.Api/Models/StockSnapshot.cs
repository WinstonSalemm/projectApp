namespace ProjectApp.Api.Models;

public class StockSnapshot
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public decimal NdQty { get; set; }
    public decimal ImQty { get; set; }
    public decimal TotalQty { get; set; }
    public DateTime CreatedAt { get; set; }
}
