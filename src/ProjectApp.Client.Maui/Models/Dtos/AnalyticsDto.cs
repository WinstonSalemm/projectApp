using System;
using System.Collections.Generic;

namespace ProjectApp.Client.Maui.Models.Dtos;

public class ManagerKpiDto
{
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int SalesCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageCheck { get; set; }
    public decimal ConversionRate { get; set; }
    public int ClientsCount { get; set; }
    public decimal BonusAmount { get; set; }
    public decimal EfficiencyScore { get; set; }
    public int Rank { get; set; }
}

public class CommissionAgentDto
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public decimal CommissionBalance { get; set; }
    public DateTime? CommissionAgentSince { get; set; }
    public string? CommissionNotes { get; set; }
}

public class CommissionStatsDto
{
    public int AgentId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public decimal TotalAccrued { get; set; }
    public decimal TotalPaid { get; set; }
    public int SalesCount { get; set; }
    public int ContractsCount { get; set; }
}

public class CommissionTransactionDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int? RelatedSaleId { get; set; }
    public int? RelatedContractId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public class AbcAnalysisDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal RevenuePercent { get; set; }
    public decimal CumulativePercent { get; set; }
    public int QtyInStock { get; set; }
    public decimal Turnover { get; set; }
    public string Recommendation { get; set; } = string.Empty;
}

public class DemandForecastDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal AvgDailySales { get; set; }
    public decimal ForecastedDemand { get; set; }
    public int CurrentStock { get; set; }
    public int DaysOfStock { get; set; }
    public string Trend { get; set; } = string.Empty;
    public bool IsCritical { get; set; }
    public string Recommendation { get; set; } = string.Empty;
}
