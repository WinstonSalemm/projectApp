namespace ProjectApp.Api.Modules.Inventory.Models;

public sealed class InventoryBatch
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public string Code { get; set; } = string.Empty; // e.g., B-2025-001 or Supply code
    public decimal UnitCost { get; set; }
    public decimal QtyReceived { get; set; }
    public decimal QtyReserved { get; set; }
    public decimal QtySold { get; set; }
    public decimal QtyReturnedIn { get; set; }
    public decimal QtyReturnedOut { get; set; }
    public decimal QtyAdjusted { get; set; }
    public decimal QtyRemaining => QtyReceived - QtySold - QtyReserved + QtyReturnedIn - QtyReturnedOut - QtyAdjusted;
    public long? SupplyId { get; set; }
    public string? SupplierName { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime PurchaseDate { get; set; }
    public decimal? VatRate { get; set; }
    public string Register { get; set; } = "ND40"; // ND40 free / IM40 reserve
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
