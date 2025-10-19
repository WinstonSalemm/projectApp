namespace ProjectApp.Api.Models;

/// <summary>
/// Брак - списание бракованных товаров
/// </summary>
public class DefectiveItem
{
    public int Id { get; set; }
    
    /// <summary>
    /// Товар
    /// </summary>
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    
    /// <summary>
    /// Название товара (snapshot)
    /// </summary>
    public string ProductName { get; set; } = "";
    
    /// <summary>
    /// SKU (snapshot)
    /// </summary>
    public string? Sku { get; set; }
    
    /// <summary>
    /// Количество брака
    /// </summary>
    public int Quantity { get; set; }
    
    /// <summary>
    /// Склад откуда списывается
    /// </summary>
    public StockRegister Warehouse { get; set; } = StockRegister.ND40;
    
    /// <summary>
    /// Причина брака
    /// </summary>
    public string? Reason { get; set; }
    
    /// <summary>
    /// Статус
    /// </summary>
    public DefectiveStatus Status { get; set; }
    
    /// <summary>
    /// Кто создал
    /// </summary>
    public string CreatedBy { get; set; } = "";
    
    /// <summary>
    /// Дата создания
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Кто отменил
    /// </summary>
    public string? CancelledBy { get; set; }
    
    /// <summary>
    /// Дата отмены
    /// </summary>
    public DateTime? CancelledAt { get; set; }
    
    /// <summary>
    /// Причина отмены
    /// </summary>
    public string? CancellationReason { get; set; }
}

/// <summary>
/// Статус брака
/// </summary>
public enum DefectiveStatus
{
    /// <summary>
    /// Активный (списано)
    /// </summary>
    Active = 0,
    
    /// <summary>
    /// Отменено (возвращено на склад)
    /// </summary>
    Cancelled = 1
}
