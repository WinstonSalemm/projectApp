using System.Collections.ObjectModel;

namespace ProjectApp.Api.Models;

public class Contract
{
    public int Id { get; set; }
    
    /// <summary>
    /// Тип договора: Закрытый (товар известен заранее) или Открытый (выбираем из каталога)
    /// </summary>
    public ContractType Type { get; set; } = ContractType.Closed;
    
    /// <summary>
    /// Номер договора
    /// </summary>
    public string ContractNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Клиент по договору
    /// </summary>
    public int? ClientId { get; set; }
    
    /// <summary>
    /// Старые поля для совместимости (будут удалены позже)
    /// </summary>
    public string OrgName { get; set; } = string.Empty;
    public string? Inn { get; set; }
    public string? Phone { get; set; }
    
    public ContractStatus Status { get; set; } = ContractStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    
    /// <summary>
    /// Примечание/описание (для закрытых договоров - описание товара который ещё не в НД)
    /// </summary>
    public string? Note { get; set; }
    
    /// <summary>
    /// Дополнительное описание для закрытых договоров
    /// </summary>
    public string? Description { get; set; }

    // Финансовые данные
    public decimal TotalAmount { get; set; }  // Общая сумма договора (для открытых - лимит)
    public decimal PaidAmount { get; set; }   // Оплачено
    public decimal ShippedAmount { get; set; } // Отгружено (сумма)

    // Данные по отгрузке (в количестве позиций)
    public int TotalItemsCount { get; set; }      // Всего позиций
    public int DeliveredItemsCount { get; set; }  // Отгружено позиций

    // ===== ПАРТНЕРСКАЯ ПРОГРАММА =====
    
    /// <summary>
    /// ID клиента-партнера, который привел этого клиента
    /// Если указан - начисляется комиссия
    /// </summary>
    public int? CommissionAgentId { get; set; }
    
    /// <summary>
    /// Сумма комиссии партнеру по этому договору (вводится вручную)
    /// </summary>
    public decimal? CommissionAmount { get; set; }

    public List<ContractItem> Items { get; set; } = new();
    public List<ContractPayment> Payments { get; set; } = new();
    public List<ContractDelivery> Deliveries { get; set; } = new();
}
