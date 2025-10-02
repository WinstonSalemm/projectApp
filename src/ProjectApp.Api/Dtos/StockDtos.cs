namespace ProjectApp.Api.Dtos;

public class StockViewDto
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Nd40Qty { get; set; }
    public decimal Im40Qty { get; set; }
    public decimal TotalQty { get; set; }
}

public class BatchStockViewDto
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Register { get; set; } = string.Empty; // ND40 or IM40
    public string? Code { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Note { get; set; }
}
