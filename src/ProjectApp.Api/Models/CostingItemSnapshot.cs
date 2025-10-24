using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ProjectApp.Api.Models;

public class CostingItemSnapshot
{
    public int Id { get; set; }
    
    public int CostingSessionId { get; set; }
    public CostingSession CostingSession { get; set; } = null!;
    
    public int SupplyItemId { get; set; }
    public SupplyItem SupplyItem { get; set; } = null!;
    
    // Снятые данные на момент расчёта
    [Required]
    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;
    
    public int Quantity { get; set; }
    
    [Precision(18, 4)]
    public decimal PriceRub { get; set; }
    
    [Precision(18, 4)]
    public decimal PriceUzs { get; set; } // PriceRub * ExchangeRate
    
    // Процентные статьи (к «цена сум»)
    [Precision(18, 4)]
    public decimal VatUzs { get; set; }
    
    [Precision(18, 4)]
    public decimal LogisticsUzs { get; set; }
    
    [Precision(18, 4)]
    public decimal StorageUzs { get; set; }
    
    [Precision(18, 4)]
    public decimal DeclarationUzs { get; set; }
    
    [Precision(18, 4)]
    public decimal CertificationUzs { get; set; }
    
    [Precision(18, 4)]
    public decimal MChsUzs { get; set; }
    
    [Precision(18, 4)]
    public decimal UnforeseenUzs { get; set; }
    
    // Абсолюты (распределены по шт)
    [Precision(18, 4)]
    public decimal CustomsUzs { get; set; }
    
    [Precision(18, 4)]
    public decimal LoadingUzs { get; set; }
    
    [Precision(18, 4)]
    public decimal ReturnsUzs { get; set; }
    
    [Precision(18, 4)]
    public decimal TotalCostUzs { get; set; } // Итог по позиции
    
    [Precision(18, 4)]
    public decimal UnitCostUzs { get; set; } // Себестоимость за 1 шт
}
