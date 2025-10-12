using System.ComponentModel.DataAnnotations;

namespace ProjectApp.Api.Modules.Finance.Models;

public class TaxPayment
{
    public int Id { get; set; }
    [Required]
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    [Range(0, double.MaxValue)]
    public decimal Amount { get; set; }
    [MaxLength(64)]
    public string Type { get; set; } = "VAT"; // VAT, ProfitTax, IncomeTax
    [MaxLength(256)]
    public string? Note { get; set; }
}
