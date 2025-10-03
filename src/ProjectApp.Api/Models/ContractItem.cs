namespace ProjectApp.Api.Models;

public class ContractItem
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public int? ProductId { get; set; } // optional link to catalog
    public string Name { get; set; } = string.Empty; // snapshot name
    public string Unit { get; set; } = "шт";
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
}
