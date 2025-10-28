namespace ProjectApp.Api.Models;

/// <summary>
/// Связь позиции договора с партией товара (для отмены и возврата товара)
/// </summary>
public class ContractReservation
{
    public int Id { get; set; }
    
    /// <summary>
    /// ID позиции договора
    /// </summary>
    public int ContractItemId { get; set; }
    public ContractItem ContractItem { get; set; } = default!;
    
    /// <summary>
    /// ID партии из которой зарезервирован товар
    /// </summary>
    public int BatchId { get; set; }
    public Batch Batch { get; set; } = default!;
    
    /// <summary>
    /// Зарезервированное количество из этой партии
    /// </summary>
    public decimal ReservedQty { get; set; }
    
    /// <summary>
    /// Дата резервирования
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Дата возврата (если договор отменён)
    /// </summary>
    public DateTime? ReturnedAt { get; set; }
    
    /// <summary>
    /// Возвращён ли товар обратно в партию
    /// </summary>
    public bool IsReturned => ReturnedAt.HasValue;
}
