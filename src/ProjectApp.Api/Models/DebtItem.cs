namespace ProjectApp.Api.Models;

/// <summary>
/// Товар в составе долга (можно редактировать цену и количество)
/// </summary>
public class DebtItem
{
    public int Id { get; set; }
    public int DebtId { get; set; }
    
    // Информация о товаре (копируется из SaleItem при создании)
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    
    // Цена и количество (можно редактировать!)
    public decimal Qty { get; set; }
    public decimal Price { get; set; }
    public decimal Total { get; set; }  // Qty * Price
    
    // Аудит
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
