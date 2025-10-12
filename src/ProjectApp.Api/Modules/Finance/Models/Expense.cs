using System.ComponentModel.DataAnnotations;

namespace ProjectApp.Api.Modules.Finance.Models;

public class Expense
{
    public int Id { get; set; }
    [Required]
    [MaxLength(64)]
    public string Category { get; set; } = "Other"; // Rent, Payroll, Transport, Marketing, Other
    [Required]
    public DateTime Date { get; set; } = DateTime.UtcNow;
    [Range(0, double.MaxValue)]
    public decimal Amount { get; set; }
    [MaxLength(512)]
    public string? Note { get; set; }
}
