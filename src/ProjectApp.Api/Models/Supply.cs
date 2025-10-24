using System.ComponentModel.DataAnnotations;

namespace ProjectApp.Api.Models;

public class Supply
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Code { get; set; } = string.Empty; // № ГТД, ввод вручную
    
    public RegisterType RegisterType { get; set; } = RegisterType.ND40; // создаётся в ND-40
    
    public SupplyStatus Status { get; set; } = SupplyStatus.HasStock;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public List<SupplyItem> Items { get; set; } = new();
    
    public List<CostingSession> CostingSessions { get; set; } = new();
}
