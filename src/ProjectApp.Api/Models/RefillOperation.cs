namespace ProjectApp.Api.Models;

/// <summary>
/// Перезарядка - перезарядка огнетушителей и подобных товаров
/// </summary>
public class RefillOperation
{
    public int Id { get; set; }
    
    /// <summary>
    /// Товар (огнетушитель)
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
    /// Количество перезаряженных единиц
    /// </summary>
    public int Quantity { get; set; }
    
    /// <summary>
    /// Склад
    /// </summary>
    public StockRegister Warehouse { get; set; } = StockRegister.ND40;
    
    /// <summary>
    /// Стоимость перезарядки за единицу
    /// </summary>
    public decimal CostPerUnit { get; set; }
    
    /// <summary>
    /// Общая стоимость = Quantity * CostPerUnit
    /// </summary>
    public decimal TotalCost { get; set; }
    
    /// <summary>
    /// Примечание
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Статус
    /// </summary>
    public RefillStatus Status { get; set; }
    
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
/// Статус перезарядки
/// </summary>
public enum RefillStatus
{
    /// <summary>
    /// Активная (перезаряжено)
    /// </summary>
    Active = 0,
    
    /// <summary>
    /// Отменена
    /// </summary>
    Cancelled = 1
}
