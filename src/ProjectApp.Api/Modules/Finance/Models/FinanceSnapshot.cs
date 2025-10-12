using System.ComponentModel.DataAnnotations;

namespace ProjectApp.Api.Modules.Finance.Models;

public class FinanceSnapshot
{
    public int Id { get; set; }
    [Required]
    public DateTime Date { get; set; } // UTC date start
    public decimal Revenue { get; set; }
    public decimal Cogs { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal Expenses { get; set; }
    public decimal TaxesPaid { get; set; }
    public decimal NetProfit { get; set; }
    public int SalesCount { get; set; }
    public int UniqueClients { get; set; }
    public decimal AverageInventory { get; set; }
}
