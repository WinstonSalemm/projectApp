using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectApp.Client.Maui.Models.Dtos;

namespace ProjectApp.Client.Maui.Services;

/// <summary>
/// API Service for finances (cashboxes, transactions, expenses, dashboard)
/// </summary>
public class FinancesApiService
{
    private readonly ApiService _apiService;

    public FinancesApiService(ApiService apiService)
    {
        _apiService = apiService;
    }

    #region Cashboxes

    /// <summary>
    /// Get all cashboxes
    /// </summary>
    public async Task<List<CashboxDto>> GetCashboxesAsync()
    {
        try
        {
            var cashboxes = await _apiService.GetAsync<List<CashboxDto>>("/api/cashboxes");
            return cashboxes ?? new List<CashboxDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FinancesApiService] GetCashboxesAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Get all cashbox balances
    /// </summary>
    public async Task<List<CashboxBalanceDto>> GetCashboxBalancesAsync()
    {
        try
        {
            var balances = await _apiService.GetAsync<List<CashboxBalanceDto>>("/api/cashboxes/balances");
            return balances ?? new List<CashboxBalanceDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FinancesApiService] GetCashboxBalancesAsync error: {ex}");
            throw;
        }
    }

    #endregion

    #region Transactions

    /// <summary>
    /// Get cash transactions with optional filters
    /// </summary>
    public async Task<List<CashTransactionDto>> GetTransactionsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? cashboxId = null)
    {
        try
        {
            var query = "/api/cash-transactions?";
            if (startDate.HasValue)
                query += $"startDate={startDate.Value:yyyy-MM-dd}&";
            if (endDate.HasValue)
                query += $"endDate={endDate.Value:yyyy-MM-dd}&";
            if (cashboxId.HasValue)
                query += $"cashboxId={cashboxId.Value}";

            var transactions = await _apiService.GetAsync<List<CashTransactionDto>>(query.TrimEnd('&', '?'));
            return transactions ?? new List<CashTransactionDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FinancesApiService] GetTransactionsAsync error: {ex}");
            throw;
        }
    }

    #endregion

    #region Operating Expenses

    /// <summary>
    /// Get operating expenses with optional filters
    /// </summary>
    public async Task<List<OperatingExpenseDto>> GetExpensesAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? type = null,
        string? status = null)
    {
        try
        {
            var query = "/api/operating-expenses?";
            if (startDate.HasValue)
                query += $"startDate={startDate.Value:yyyy-MM-dd}&";
            if (endDate.HasValue)
                query += $"endDate={endDate.Value:yyyy-MM-dd}&";
            if (!string.IsNullOrEmpty(type))
                query += $"type={type}&";
            if (!string.IsNullOrEmpty(status))
                query += $"status={status}";

            var expenses = await _apiService.GetAsync<List<OperatingExpenseDto>>(query.TrimEnd('&', '?'));
            return expenses ?? new List<OperatingExpenseDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FinancesApiService] GetExpensesAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Get expenses grouped by type
    /// </summary>
    public async Task<Dictionary<string, decimal>> GetExpensesByTypeAsync(
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        try
        {
            var query = "/api/operating-expenses/by-type?";
            if (startDate.HasValue)
                query += $"startDate={startDate.Value:yyyy-MM-dd}&";
            if (endDate.HasValue)
                query += $"endDate={endDate.Value:yyyy-MM-dd}";

            var expenses = await _apiService.GetAsync<Dictionary<string, decimal>>(query.TrimEnd('&', '?'));
            return expenses ?? new Dictionary<string, decimal>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FinancesApiService] GetExpensesByTypeAsync error: {ex}");
            throw;
        }
    }

    #endregion

    #region Owner Dashboard

    /// <summary>
    /// Get owner dashboard data
    /// </summary>
    public async Task<OwnerDashboardDto?> GetOwnerDashboardAsync()
    {
        try
        {
            return await _apiService.GetAsync<OwnerDashboardDto>("/api/owner-dashboard");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FinancesApiService] GetOwnerDashboardAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Get P&L report
    /// </summary>
    public async Task<PLReportDto?> GetPLReportAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var query = $"/api/owner-dashboard/pl-report?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}";
            return await _apiService.GetAsync<PLReportDto>(query);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FinancesApiService] GetPLReportAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Get Cash Flow report
    /// </summary>
    public async Task<CashFlowReportDto?> GetCashFlowReportAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var query = $"/api/owner-dashboard/cashflow-report?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}";
            return await _apiService.GetAsync<CashFlowReportDto>(query);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FinancesApiService] GetCashFlowReportAsync error: {ex}");
            throw;
        }
    }

    #endregion
}

public class PLReportDto
{
    public decimal Revenue { get; set; }
    public decimal Cogs { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal OperatingExpenses { get; set; }
    public decimal Ebitda { get; set; }
    public decimal NetProfit { get; set; }
}

public class CashFlowReportDto
{
    public decimal OperatingCashFlow { get; set; }
    public decimal InvestingCashFlow { get; set; }
    public decimal FinancingCashFlow { get; set; }
    public decimal NetCashFlow { get; set; }
}
