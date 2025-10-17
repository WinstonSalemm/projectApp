namespace ProjectApp.Api.Models;

/// <summary>
/// Касса/счет для учета денежных средств
/// </summary>
public class Cashbox
{
    public int Id { get; set; }
    
    /// <summary>
    /// Название кассы (например: "Офис - Главная касса", "Склад ND-40", "Менеджер Иван")
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Тип кассы
    /// </summary>
    public CashboxType Type { get; set; }
    
    /// <summary>
    /// Валюта
    /// </summary>
    public string Currency { get; set; } = "UZS";
    
    /// <summary>
    /// Текущий баланс
    /// </summary>
    public decimal CurrentBalance { get; set; }
    
    /// <summary>
    /// Ответственное лицо (Username)
    /// </summary>
    public string? ResponsibleUser { get; set; }
    
    /// <summary>
    /// Активна ли касса
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Описание
    /// </summary>
    public string? Description { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Тип кассы
/// </summary>
public enum CashboxType
{
    Office = 0,      // Офисная касса
    Warehouse = 1,   // Складская касса
    Manager = 2,     // У менеджера на руках
    BankAccount = 3, // Банковский счет
    CryptoWallet = 4 // Криптокошелек
}
