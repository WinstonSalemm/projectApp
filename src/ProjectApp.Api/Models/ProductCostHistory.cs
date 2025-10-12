namespace ProjectApp.Api.Models;

public class ProductCostHistory
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime SnapshotAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
}
