namespace ProjectApp.Api.Dtos;

public class ContractItemDto
{
    public int? ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "шт";
    public decimal Qty { get; set; }
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
    public List<ContractItemDto> Items { get; set; } = new();
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
