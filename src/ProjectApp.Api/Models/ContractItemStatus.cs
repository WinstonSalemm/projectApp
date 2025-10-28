namespace ProjectApp.Api.Models;

/// <summary>
/// Статус позиции договора
/// </summary>
public enum ContractItemStatus
{
    /// <summary>
    /// Зарезервировано - товар изъят из партий и ждёт отгрузки
    /// </summary>
    Reserved = 0,
    
    /// <summary>
    /// Отгружено - товар выдан клиенту
    /// </summary>
    Shipped = 1,
    
    /// <summary>
    /// Отменено - договор расторгнут, товар возвращён в партии
    /// </summary>
    Cancelled = 2
}
