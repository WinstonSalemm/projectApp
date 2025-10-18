using System;

namespace ProjectApp.Client.Maui.Models.Dtos;

public class DebtorDto
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public decimal TotalDebt { get; set; }
    public int DebtsCount { get; set; }
    public DateTime? OldestDueDate { get; set; }
}

public class DebtDetailsDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal OriginalAmount { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public List<DebtItemDto> Items { get; set; } = new();
}

public class DebtItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int Qty { get; set; }
    public decimal Price { get; set; }
    public decimal Total { get; set; }
}

public class PayDebtRequest
{
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string? Notes { get; set; }
}
