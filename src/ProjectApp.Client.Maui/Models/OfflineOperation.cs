using SQLite;

namespace ProjectApp.Client.Maui.Models;

/// <summary>
/// Локально сохраненная операция для синхронизации с сервером
/// </summary>
[Table("OfflineOperations")]
public class OfflineOperation
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    /// <summary>
    /// Тип операции: Sale, Supply, Transfer, Defective, Refill
    /// </summary>
    public string OperationType { get; set; } = string.Empty;
    
    /// <summary>
    /// JSON данных операции
    /// </summary>
    public string PayloadJson { get; set; } = string.Empty;
    
    /// <summary>
    /// Время создания операции (реальное время, когда пользователь её сделал)
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Кто создал операцию
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;
    
    /// <summary>
    /// Статус: Pending, Syncing, Synced, Failed
    /// </summary>
    public string Status { get; set; } = "Pending";
    
    /// <summary>
    /// Количество попыток синхронизации
    /// </summary>
    public int RetryCount { get; set; }
    
    /// <summary>
    /// Последняя ошибка синхронизации
    /// </summary>
    public string? LastError { get; set; }
    
    /// <summary>
    /// Время последней попытки синхронизации
    /// </summary>
    public DateTime? LastSyncAttempt { get; set; }
    
    /// <summary>
    /// ID созданной записи на сервере (после успешной синхронизации)
    /// </summary>
    public int? ServerRecordId { get; set; }
}

/// <summary>
/// Типы операций для офлайн режима
/// </summary>
public static class OfflineOperationType
{
    public const string Sale = "Sale";
    public const string Supply = "Supply";
    public const string Transfer = "Transfer";
    public const string Defective = "Defective";
    public const string Refill = "Refill";
}

/// <summary>
/// Статусы синхронизации
/// </summary>
public static class SyncStatus
{
    public const string Pending = "Pending";      // Ожидает синхронизации
    public const string Syncing = "Syncing";      // В процессе синхронизации
    public const string Synced = "Synced";        // Успешно синхронизировано
    public const string Failed = "Failed";        // Ошибка синхронизации
}
