namespace ProjectApp.Api.Models;

/// <summary>
/// Типы налогов в Узбекистане
/// </summary>
public enum TaxType
{
    VAT = 1,              // НДС (12%)
    IncomeTax = 2,        // Налог на прибыль (15%)
    SocialTax = 3,        // Единый социальный платеж (12%)
    PropertyTax = 4,      // Налог на имущество
    LandTax = 5,          // Земельный налог
    WaterTax = 6,         // Налог за пользование водными ресурсами
    INPS = 7,             // ИНПС (0.2%)
    SchoolFund = 8,       // Отчисления в школьный фонд (1.5%)
    SimplifiedTax = 9     // Упрощенный налог (4-7.5%)
}

/// <summary>
/// Система налогообложения
/// </summary>
public enum TaxSystem
{
    General = 1,      // Общая система
    Simplified = 2    // Упрощенная система
}

/// <summary>
/// Период налогообложения
/// </summary>
public enum TaxPeriod
{
    Monthly = 1,   // Ежемесячно
    Quarterly = 2, // Ежеквартально
    Yearly = 3     // Ежегодно
}

/// <summary>
/// Налоговая запись
/// </summary>
public class TaxRecord
{
    public int Id { get; set; }
    
    /// <summary>
    /// Тип налога
    /// </summary>
    public TaxType Type { get; set; }
    
    /// <summary>
    /// Налоговый период (месяц/квартал/год)
    /// </summary>
    public DateTime Period { get; set; }
    
    /// <summary>
    /// Налоговая база (сумма, с которой считается налог)
    /// </summary>
    public decimal TaxBase { get; set; }
    
    /// <summary>
    /// Ставка налога (%)
    /// </summary>
    public decimal TaxRate { get; set; }
    
    /// <summary>
    /// Сумма налога
    /// </summary>
    public decimal TaxAmount { get; set; }
    
    /// <summary>
    /// Оплачено
    /// </summary>
    public bool IsPaid { get; set; }
    
    /// <summary>
    /// Дата оплаты
    /// </summary>
    public DateTime? PaidAt { get; set; }
    
    /// <summary>
    /// Срок уплаты
    /// </summary>
    public DateTime DueDate { get; set; }
    
    /// <summary>
    /// Дата расчета
    /// </summary>
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Примечание
    /// </summary>
    public string? Note { get; set; }
}

/// <summary>
/// Настройки налогообложения компании
/// </summary>
public class TaxSettings
{
    public int Id { get; set; }
    
    /// <summary>
    /// Система налогообложения
    /// </summary>
    public TaxSystem System { get; set; } = TaxSystem.General;
    
    /// <summary>
    /// ИНН компании
    /// </summary>
    public string? CompanyINN { get; set; }
    
    /// <summary>
    /// Название компании
    /// </summary>
    public string? CompanyName { get; set; }
    
    /// <summary>
    /// Ставка НДС (по умолчанию 12%)
    /// </summary>
    public decimal VATRate { get; set; } = 12m;
    
    /// <summary>
    /// Ставка налога на прибыль (по умолчанию 15%)
    /// </summary>
    public decimal IncomeTaxRate { get; set; } = 15m;
    
    /// <summary>
    /// Ставка единого социального платежа (по умолчанию 12%)
    /// </summary>
    public decimal SocialTaxRate { get; set; } = 12m;
    
    /// <summary>
    /// Ставка ИНПС (по умолчанию 0.2%)
    /// </summary>
    public decimal INPSRate { get; set; } = 0.2m;
    
    /// <summary>
    /// Ставка школьного фонда (по умолчанию 1.5%)
    /// </summary>
    public decimal SchoolFundRate { get; set; } = 1.5m;
    
    /// <summary>
    /// Ставка упрощенного налога (для УСН, 4-7.5%)
    /// </summary>
    public decimal SimplifiedTaxRate { get; set; } = 4m;
    
    /// <summary>
    /// Включен ли НДС (плательщик НДС или нет)
    /// </summary>
    public bool IsVATRegistered { get; set; } = true;
    
    /// <summary>
    /// Дата последнего обновления
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
