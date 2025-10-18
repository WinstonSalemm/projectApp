using System.Collections.ObjectModel;

namespace ProjectApp.Api.Models;

public enum SaleCategory
{
    White = 0,  // Белая (официальная, с чеком)
    Grey = 1,   // Серая (частично оформленная)
    Black = 2   // Черная (неофициальная)
}

public class Sale
{
    public int Id { get; set; }
    public int? ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public List<SaleItem> Items { get; set; } = new();
    public PaymentType PaymentType { get; set; }
    public SaleCategory Category { get; set; } = SaleCategory.White; // По умолчанию белая
    public decimal Total { get; set; }
    
    /// <summary>
    /// ID клиента-партнера (агента), который привел покупателя
    /// Если указан - начисляется комиссия
    /// </summary>
    public int? CommissionAgentId { get; set; }
    
    /// <summary>
    /// Процент комиссии для партнера (вводится вручную)
    /// Например: 5.0 = 5% от суммы продажи
    /// </summary>
    public decimal? CommissionRate { get; set; }
    
    /// <summary>
    /// Сумма комиссии партнеру
    /// Рассчитывается как Total * CommissionRate / 100
    /// </summary>
    public decimal? CommissionAmount { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? ReservationNotes { get; set; }
}
