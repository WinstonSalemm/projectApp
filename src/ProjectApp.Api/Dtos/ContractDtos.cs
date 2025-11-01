namespace ProjectApp.Api.Dtos;

public class ContractItemDto
{
    public int Id { get; set; }
    public int? ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }  // Для товаров которых нет в каталоге
    public string Unit { get; set; } = "шт";
    public decimal Qty { get; set; }
    public decimal DeliveredQty { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Status { get; set; }  // Reserved, Shipped, Cancelled
}

public class ContractDto
{
    public int Id { get; set; }
    public string Type { get; set; } = "Closed";  // Closed, Open
    public string ContractNumber { get; set; } = string.Empty;
    public int? ClientId { get; set; }
    public string OrgName { get; set; } = string.Empty;  // Legacy
    public string? Inn { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = "Signed";
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? Note { get; set; }
    public string? Description { get; set; }  // Для закрытых - описание товара
    public decimal TotalAmount { get; set; }  // Для открытых - лимит
    public decimal PaidAmount { get; set; }
    public decimal ShippedAmount { get; set; }  // Сумма отгруженного
    public int TotalItemsCount { get; set; }
    public int DeliveredItemsCount { get; set; }
    public decimal Balance => PaidAmount - ShippedAmount; // Баланс: Оплачено - Забрано
    
    // Computed fields для UI
    public decimal BalanceDue => TotalAmount - PaidAmount;  // Долг
    public decimal PaidPercent => TotalAmount > 0 ? (PaidAmount / TotalAmount * 100) : 0;
    public decimal ShippedPercent => TotalAmount > 0 ? (ShippedAmount / TotalAmount * 100) : 0;
    
    public List<ContractItemDto> Items { get; set; } = new();
    public List<ContractPaymentDto> Payments { get; set; } = new();
    public List<ContractDeliveryDto> Deliveries { get; set; } = new();
}

public class ContractPaymentDto
{
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public DateTime PaidAt { get; set; }
    public string? Note { get; set; }
}

public class ContractDeliveryDto
{
    public int Id { get; set; }
    public int ContractItemId { get; set; }
    public decimal Qty { get; set; }
    public DateTime DeliveredAt { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = "Completed";
}

public class ContractCreateDto
{
    public string Type { get; set; } = "Closed";  // Closed, Open
    public string ContractNumber { get; set; } = string.Empty;
    public int? ClientId { get; set; }
    public string OrgName { get; set; } = string.Empty;  // Legacy fallback
    public string? Inn { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = "Active";
    public string? Note { get; set; }
    public string? Description { get; set; }  // Для закрытых договоров
    public decimal? TotalAmount { get; set; }  // Для открытых - указывается лимит
    public decimal? LimitTotalUzs { get; set; }
    public int? StoreId { get; set; }
    public int? ManagerId { get; set; }
    public List<ContractItemDto> Items { get; set; } = new();
    
    // ПАРТНЕРСКАЯ ПРОГРАММА
    /// <summary>
    /// ID клиента-партнера, который привел этого клиента
    /// </summary>
    public int? CommissionAgentId { get; set; }
    
    /// <summary>
    /// Сумма комиссии партнеру (вводится вручную)
    /// </summary>
    public decimal? CommissionAmount { get; set; }
}

public class ContractUpdateStatusDto
{
    public string Status { get; set; } = "Signed";
}

public class ContractPaymentCreateDto
{
    public decimal Amount { get; set; }
    public string Method { get; set; } = "BankTransfer"; // Cash, BankTransfer, Card, Click, Payme
    public string? Note { get; set; }
}

public class ContractDeliveryCreateDto
{
    public int ContractItemId { get; set; }
    public decimal Qty { get; set; }
    public string? Note { get; set; }
}
