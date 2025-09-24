namespace ProjectApp.Api.Models;

public enum StockRegister
{
    ND40 = 0,
    IM40 = 1
}

public class Stock
{
    public int ProductId { get; set; }
    public StockRegister Register { get; set; }
    public decimal Qty { get; set; }
}
