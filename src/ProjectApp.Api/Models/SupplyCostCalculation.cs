namespace ProjectApp.Api.Models;

/// <summary>
/// Параметры для расчета себестоимости НД-40 (предварительный расчет)
/// </summary>
public class SupplyCostCalculation
{
    public int Id { get; set; }
    
    // Ссылка на партию (batch)
    public int? BatchId { get; set; }
    
    // === ГЛОБАЛЬНЫЕ ПАРАМЕТРЫ ПОСТАВКИ ===
    /// <summary>
    /// Курс (смешанная) какой, руб или $
    /// </summary>
    public decimal ExchangeRate { get; set; }
    
    /// <summary>
    /// Таможенный сбор (фикс. сумма)
    /// </summary>
    public decimal CustomsFee { get; set; }
    
    /// <summary>
    /// НДС % (например, 22%)
    /// </summary>
    public decimal VatPercent { get; set; }
    
    /// <summary>
    /// Корректива % (например, 0.50%)
    /// </summary>
    public decimal CorrectionPercent { get; set; }
    
    /// <summary>
    /// Охрана % (например, 0.2%)
    /// </summary>
    public decimal SecurityPercent { get; set; }
    
    /// <summary>
    /// Декларация % (например, 1%)
    /// </summary>
    public decimal DeclarationPercent { get; set; }
    
    /// <summary>
    /// Сертификация % (например, 1%)
    /// </summary>
    public decimal CertificationPercent { get; set; }
    
    /// <summary>
    /// База для расчета (например, 10000000.00)
    /// </summary>
    public decimal CalculationBase { get; set; }
    
    /// <summary>
    /// Погрузка % (например, 1.6%)
    /// </summary>
    public decimal LoadingPercent { get; set; }
    
    // === ПАРАМЕТРЫ ПОЗИЦИИ ===
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    
    /// <summary>
    /// Количество
    /// </summary>
    public decimal Quantity { get; set; }
    
    /// <summary>
    /// Цена в рублях за единицу
    /// </summary>
    public decimal PriceRub { get; set; }
    
    /// <summary>
    /// Цена в суммах (Quantity * PriceRub)
    /// </summary>
    public decimal PriceTotal { get; set; }
    
    /// <summary>
    /// Вес (для расчета логистики)
    /// </summary>
    public decimal? Weight { get; set; }
    
    // === РАССЧИТАННЫЕ ЗНАЧЕНИЯ ===
    /// <summary>
    /// Таможня (рассчитывается)
    /// </summary>
    public decimal CustomsAmount { get; set; }
    
    /// <summary>
    /// НДС (таможня)
    /// </summary>
    public decimal VatAmount { get; set; }
    
    /// <summary>
    /// Корректива
    /// </summary>
    public decimal CorrectionAmount { get; set; }
    
    /// <summary>
    /// Охрана
    /// </summary>
    public decimal SecurityAmount { get; set; }
    
    /// <summary>
    /// Декларация
    /// </summary>
    public decimal DeclarationAmount { get; set; }
    
    /// <summary>
    /// Сертификация
    /// </summary>
    public decimal CertificationAmount { get; set; }
    
    /// <summary>
    /// Погрузка
    /// </summary>
    public decimal LoadingAmount { get; set; }
    
    /// <summary>
    /// Отклонение (если есть)
    /// </summary>
    public decimal? DeviationAmount { get; set; }
    
    /// <summary>
    /// ИТОГОВЫЙ СЕБЕС ЗАКУПКИ (на все количество)
    /// </summary>
    public decimal TotalCost { get; set; }
    
    /// <summary>
    /// Себестоимость за единицу (TotalCost / Quantity)
    /// </summary>
    public decimal UnitCost { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? Notes { get; set; }
}
