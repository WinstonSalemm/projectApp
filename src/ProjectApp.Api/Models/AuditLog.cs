namespace ProjectApp.Api.Models;

/// <summary>
/// Аудит-лог действий пользователей
/// </summary>
public class AuditLog
{
    public int Id { get; set; }
    
    /// <summary>
    /// Имя пользователя
    /// </summary>
    public string UserName { get; set; } = string.Empty;
    
    /// <summary>
    /// Действие (Create, Update, Delete, etc.)
    /// </summary>
    public string Action { get; set; } = string.Empty;
    
    /// <summary>
    /// Тип сущности (Sale, Product, Client, etc.)
    /// </summary>
    public string EntityType { get; set; } = string.Empty;
    
    /// <summary>
    /// ID сущности
    /// </summary>
    public int? EntityId { get; set; }
    
    /// <summary>
    /// Старое значение (JSON)
    /// </summary>
    public string? OldValue { get; set; }
    
    /// <summary>
    /// Новое значение (JSON)
    /// </summary>
    public string? NewValue { get; set; }
    
    /// <summary>
    /// IP адрес
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// User Agent
    /// </summary>
    public string? UserAgent { get; set; }
    
    /// <summary>
    /// Дата и время
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Дополнительная информация
    /// </summary>
    public string? Details { get; set; }
}
