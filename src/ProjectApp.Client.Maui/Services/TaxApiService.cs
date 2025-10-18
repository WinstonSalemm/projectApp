using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectApp.Client.Maui.Models.Dtos;

namespace ProjectApp.Client.Maui.Services;

/// <summary>
/// API Service for tax analytics and calculations (Uzbekistan tax system)
/// </summary>
public class TaxApiService
{
    private readonly ApiService _apiService;

    public TaxApiService(ApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Get tax report for a specific period
    /// </summary>
    public async Task<TaxReportDto?> GetTaxReportAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var query = $"/api/tax-analytics/report?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}";
            return await _apiService.GetAsync<TaxReportDto>(query);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TaxApiService] GetTaxReportAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Get monthly tax report
    /// </summary>
    public async Task<TaxReportDto?> GetMonthlyTaxReportAsync(int year, int month)
    {
        try
        {
            var query = $"/api/tax-analytics/report/monthly?year={year}&month={month}";
            return await _apiService.GetAsync<TaxReportDto>(query);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TaxApiService] GetMonthlyTaxReportAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Get quarterly tax report
    /// </summary>
    public async Task<TaxReportDto?> GetQuarterlyTaxReportAsync(int year, int quarter)
    {
        try
        {
            var query = $"/api/tax-analytics/report/quarterly?year={year}&quarter={quarter}";
            return await _apiService.GetAsync<TaxReportDto>(query);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TaxApiService] GetQuarterlyTaxReportAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Get yearly tax report
    /// </summary>
    public async Task<TaxReportDto?> GetYearlyTaxReportAsync(int year)
    {
        try
        {
            var query = $"/api/tax-analytics/report/yearly?year={year}";
            return await _apiService.GetAsync<TaxReportDto>(query);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TaxApiService] GetYearlyTaxReportAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Get unpaid taxes
    /// </summary>
    public async Task<List<TaxRecordDto>> GetUnpaidTaxesAsync()
    {
        try
        {
            var taxes = await _apiService.GetAsync<List<TaxRecordDto>>("/api/tax-analytics/unpaid");
            return taxes ?? new List<TaxRecordDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TaxApiService] GetUnpaidTaxesAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Mark tax as paid
    /// </summary>
    public async Task MarkTaxAsPaidAsync(int taxRecordId)
    {
        try
        {
            await _apiService.PostAsync<object>($"/api/tax-analytics/{taxRecordId}/mark-paid", new { });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TaxApiService] MarkTaxAsPaidAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Get tax settings
    /// </summary>
    public async Task<TaxSettingsDto?> GetTaxSettingsAsync()
    {
        try
        {
            return await _apiService.GetAsync<TaxSettingsDto>("/api/tax-analytics/settings");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TaxApiService] GetTaxSettingsAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Calculate VAT from amount (extract 12%)
    /// </summary>
    public async Task<VatCalculationDto?> CalculateVatAsync(decimal amount)
    {
        try
        {
            var query = $"/api/tax-analytics/calculate-vat?amount={amount}";
            return await _apiService.GetAsync<VatCalculationDto>(query);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TaxApiService] CalculateVatAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Add VAT to amount (add 12%)
    /// </summary>
    public async Task<VatCalculationDto?> AddVatAsync(decimal amount)
    {
        try
        {
            var query = $"/api/tax-analytics/add-vat?amount={amount}";
            return await _apiService.GetAsync<VatCalculationDto>(query);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TaxApiService] AddVatAsync error: {ex}");
            throw;
        }
    }
}

public class VatCalculationDto
{
    public decimal OriginalAmount { get; set; }
    public decimal AmountWithoutVat { get; set; }
    public decimal VatAmount { get; set; }
    public decimal AmountWithVat { get; set; }
}
