namespace ProjectApp.Api.Models;

/// <summary>
/// Тип договора
/// </summary>
public enum ContractType
{
    /// <summary>
    /// Закрытый - заранее известно что продаём (может быть даже товар которого нет в НД)
    /// </summary>
    Closed = 0,
    
    /// <summary>
    /// Открытый - выбираем товар из каталога по мере продажи, до определённой суммы
    /// </summary>
    Open = 1
}
