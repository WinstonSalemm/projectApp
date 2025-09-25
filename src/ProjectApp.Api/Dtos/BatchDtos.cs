namespace ProjectApp.Api.Dtos;

using ProjectApp.Api.Models;

public class BatchCreateDto
{
    public int ProductId { get; set; }
    public StockRegister Register { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
    public string? Note { get; set; }
}

public class BatchUpdateDto
{
    public decimal? UnitCost { get; set; }
    public string? Note { get; set; }
    public DateTime? CreatedAt { get; set; }
}
