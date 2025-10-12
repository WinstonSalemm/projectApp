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
    public string? Code { get; set; }
    public string? Note { get; set; }
    // Finance analytics enrichment
    public string? SupplierName { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal? VatRate { get; set; }
    public string? PurchaseSource { get; set; } // e.g., SupplyId/code
    public string? GtdCode { get; set; }
    public DateTime? ArchivedAt { get; set; }
}
