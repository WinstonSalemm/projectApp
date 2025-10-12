using System.Collections.Generic;

namespace ProjectApp.Api.Dtos;

public class SupplyLineDto
{
    public int ProductId { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
    public string Code { get; set; } = string.Empty; // batch code
    public string? Note { get; set; }
    public decimal? VatRate { get; set; }
}

public class SupplyCreateDto
{
    public List<SupplyLineDto> Items { get; set; } = new();
    public string? SupplierName { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal? VatRate { get; set; }
}

public class SupplyTransferItemDto
{
    public int ProductId { get; set; }
    public decimal Qty { get; set; }
}

public class SupplyTransferDto
{
    public List<SupplyTransferItemDto> Items { get; set; } = new();
}
