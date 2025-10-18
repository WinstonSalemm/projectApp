using System;
using System.Collections.Generic;

namespace ProjectApp.Client.Maui.Models.Dtos;

public class CashboxDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "UZS";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CashboxBalanceDto
{
    public int CashboxId { get; set; }
    public string CashboxName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "UZS";
}

public class CashTransactionDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int? FromCashboxId { get; set; }
    public string? FromCashboxName { get; set; }
    public int? ToCashboxId { get; set; }
    public string? ToCashboxName { get; set; }
    public string? Description { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? CreatedBy { get; set; }
}

public class OperatingExpenseDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidDate { get; set; }
    public int? CashboxId { get; set; }
    public string? CashboxName { get; set; }
}

public class OwnerDashboardDto
{
    public decimal TodayRevenue { get; set; }
    public decimal TodayProfit { get; set; }
    public int TodaySalesCount { get; set; }
    public decimal TotalCashboxBalance { get; set; }
    public decimal ClientDebts { get; set; }
    public decimal SupplierDebts { get; set; }
    public decimal InventoryValue { get; set; }
    public List<TopProductDto> TopProducts { get; set; } = new();
    public List<AlertDto> Alerts { get; set; } = new();
}

public class TopProductDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int QtySold { get; set; }
    public decimal Revenue { get; set; }
}

public class AlertDto
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}
