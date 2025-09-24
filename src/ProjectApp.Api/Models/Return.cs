namespace ProjectApp.Api.Models;

public class Return
{
    public int Id { get; set; }
    public int? RefSaleId { get; set; }
    public int? ClientId { get; set; }
    public decimal Sum { get; set; }
    public DateTime CreatedAt { get; set; }
}
