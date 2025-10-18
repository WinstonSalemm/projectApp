using ProjectApp.Api.Models;

namespace ProjectApp.Api.Dtos;

public class SaleCreateItemDto
{
    public int ProductId { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
}

public class SaleCreateDto
{
    public int? ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public List<SaleCreateItemDto> Items { get; set; } = new();
    public PaymentType PaymentType { get; set; }
    public List<string>? ReservationNotes { get; set; }
    // If true, API will NOT send text message immediately; client will send photo+caption instead
    public bool? NotifyHold { get; set; }
    
    // ПАРТНЕРСКАЯ ПРОГРАММА
    /// <summary>
    /// ID клиента-партнера, который привел покупателя
    /// </summary>
    public int? CommissionAgentId { get; set; }
    
    /// <summary>
    /// Процент комиссии для партнера (вводится вручную)
    /// </summary>
    public decimal? CommissionRate { get; set; }
    
    // СИСТЕМА ДОЛГОВ
    /// <summary>
    /// Срок оплаты долга (только для PaymentType = Debt)
    /// </summary>
    public DateTime? DebtDueDate { get; set; }
    
    /// <summary>
    /// Примечания к долгу
    /// </summary>
    public string? DebtNotes { get; set; }
}
