using ProjectApp.Api.Models;

namespace ProjectApp.Api.Models;

public class Batch
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public StockRegister Register { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Note { get; set; }
}
