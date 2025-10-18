using System.Collections.ObjectModel;

namespace ProjectApp.Api.Models;

public class Contract
{
    public int Id { get; set; }
    public string OrgName { get; set; } = string.Empty;
    public string? Inn { get; set; }
    public string? Phone { get; set; }
    public ContractStatus Status { get; set; } = ContractStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? Note { get; set; }

    // Финансовые данные
    public decimal TotalAmount { get; set; }  // Общая сумма договора
    public decimal PaidAmount { get; set; }   // Оплачено

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
