namespace ProjectApp.Api.Dtos;

public class ContractItemDto
{
    public int Id { get; set; }
    public int? ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "шт";
    public decimal Qty { get; set; }
    public decimal DeliveredQty { get; set; }
    public decimal UnitPrice { get; set; }
}

public class ContractDto
{
    public int Id { get; set; }
    public string OrgName { get; set; } = string.Empty;
    public string? Inn { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = "Signed";
    public DateTime CreatedAt { get; set; }
    public string? Note { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public int TotalItemsCount { get; set; }
    public int DeliveredItemsCount { get; set; }
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
    public int ContractItemId { get; set; }
    public decimal Qty { get; set; }
    public DateTime DeliveredAt { get; set; }
    public string? Note { get; set; }
}

public class ContractCreateDto
{
    public string OrgName { get; set; } = string.Empty;
    public string? Inn { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = "Signed";
    public string? Note { get; set; }
    public List<ContractItemDto> Items { get; set; } = new();
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
