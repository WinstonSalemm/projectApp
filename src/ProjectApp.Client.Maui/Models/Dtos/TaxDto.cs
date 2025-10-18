using System;
using System.Collections.Generic;

namespace ProjectApp.Client.Maui.Models.Dtos;

public class TaxReportDto
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal RevenueWithoutVAT { get; set; }
    public decimal VatFromSales { get; set; }
    public decimal VatFromPurchases { get; set; }
    public decimal VatPayable { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal OperatingExpenses { get; set; }
    public decimal Ebit { get; set; }
    public decimal IncomeTax { get; set; }
    public decimal SocialTax { get; set; }
    public decimal Inps { get; set; }
    public decimal SchoolFund { get; set; }
    public decimal TotalTaxes { get; set; }
    public decimal NetProfit { get; set; }
    public decimal NetProfitMargin { get; set; }
    public List<TaxPayableDto> TaxesPayable { get; set; } = new();
}

public class TaxPayableDto
{
    public string TaxType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class TaxRecordDto
{
    public int Id { get; set; }
    public string TaxType { get; set; } = string.Empty;
    public DateTime Period { get; set; }
    public decimal TaxBase { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidDate { get; set; }
}

public class TaxSettingsDto
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Inn { get; set; } = string.Empty;
    public string TaxSystem { get; set; } = string.Empty;
    public bool IsVatPayer { get; set; }
    public decimal VatRate { get; set; }
    public decimal IncomeTaxRate { get; set; }
    public decimal SocialTaxRate { get; set; }
    public decimal InpsRate { get; set; }
    public decimal SchoolFundRate { get; set; }
}
