using System.Collections.Generic;

namespace ProjectApp.Api.Dtos;

public class SupplyLineDto
{
    public int ProductId { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
    public string Code { get; set; } = string.Empty; // batch code
    public string? Note { get; set; }
    public decimal? VatRate { get; set; }
    
    // === ПАРАМЕТРЫ ДЛЯ РАСЧЕТА НД-40 СЕБЕСТОИМОСТИ ===
    public decimal? PriceRub { get; set; } // Цена в рублях за единицу
    public decimal? Weight { get; set; } // Вес для логистики
}

public class SupplyCreateDto
{
    public List<SupplyLineDto> Items { get; set; } = new();
    public string? SupplierName { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal? VatRate { get; set; }
    
    // === ГЛОБАЛЬНЫЕ ПАРАМЕТРЫ РАСЧЕТА НД-40 ===
    /// <summary>
    /// Курс (смешанная) какой, руб или $ (по умолчанию 158.08)
    /// </summary>
    public decimal? ExchangeRate { get; set; }
    
    /// <summary>
    /// Таможенный сбор (по умолчанию 105000)
    /// </summary>
    public decimal? CustomsFee { get; set; }
    
    /// <summary>
    /// НДС % (по умолчанию 22%)
    /// </summary>
    public decimal? VatPercent { get; set; }
    
    /// <summary>
    /// Корректива % (по умолчанию 0.50%)
    /// </summary>
    public decimal? CorrectionPercent { get; set; }
    
    /// <summary>
    /// Охрана % (по умолчанию 0.2%)
    /// </summary>
    public decimal? SecurityPercent { get; set; }
    
    /// <summary>
    /// Декларация % (по умолчанию 1%)
    /// </summary>
    public decimal? DeclarationPercent { get; set; }
    
    /// <summary>
    /// Сертификация % (по умолчанию 1%)
    /// </summary>
    public decimal? CertificationPercent { get; set; }
    
    /// <summary>
    /// База для расчета (по умолчанию 10000000.00)
    /// </summary>
    public decimal? CalculationBase { get; set; }
    
    /// <summary>
    /// Погрузка % (по умолчанию 1.6%)
    /// </summary>
    public decimal? LoadingPercent { get; set; }
}

public class SupplyTransferItemDto
{
    public int ProductId { get; set; }
    public decimal Qty { get; set; }
}

public class SupplyTransferDto
{
    public List<SupplyTransferItemDto> Items { get; set; } = new();
}

public class SupplyCostCalculationDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public decimal Quantity { get; set; }
    public decimal PriceRub { get; set; }
    public decimal PriceTotal { get; set; }
    public decimal? Weight { get; set; }
    
    // Рассчитанные значения
    public decimal CustomsAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal CorrectionAmount { get; set; }
    public decimal SecurityAmount { get; set; }
    public decimal DeclarationAmount { get; set; }
    public decimal CertificationAmount { get; set; }
    public decimal LoadingAmount { get; set; }
    public decimal? DeviationAmount { get; set; }
    
    public decimal TotalCost { get; set; }
    public decimal UnitCost { get; set; }
}

public class SupplyCostPreviewDto
{
    public List<SupplyCostCalculationDto> Items { get; set; } = new();
    public decimal GrandTotalCost { get; set; }
    
    // Глобальные параметры использованные в расчете
    public decimal ExchangeRate { get; set; }
    public decimal CustomsFee { get; set; }
    public decimal VatPercent { get; set; }
    public decimal CorrectionPercent { get; set; }
    public decimal SecurityPercent { get; set; }
    public decimal DeclarationPercent { get; set; }
    public decimal CertificationPercent { get; set; }
    public decimal CalculationBase { get; set; }
    public decimal LoadingPercent { get; set; }
}
