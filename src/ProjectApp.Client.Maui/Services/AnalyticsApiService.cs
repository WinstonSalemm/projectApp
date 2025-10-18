using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectApp.Client.Maui.Models.Dtos;

namespace ProjectApp.Client.Maui.Services;

/// <summary>
/// API Service for analytics (KPI, commission, commercial analytics)
/// </summary>
public class AnalyticsApiService
{
    private readonly ApiService _apiService;

    public AnalyticsApiService(ApiService apiService)
    {
        _apiService = apiService;
    }

    #region Manager KPI

    /// <summary>
    /// Get KPI for all managers
    /// </summary>
    public async Task<List<ManagerKpiDto>> GetAllManagerKpiAsync(
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        try
        {
            var query = "/api/manager-kpi?";
            if (startDate.HasValue)
                query += $"startDate={startDate.Value:yyyy-MM-dd}&";
            if (endDate.HasValue)
                query += $"endDate={endDate.Value:yyyy-MM-dd}";

            var kpis = await _apiService.GetAsync<List<ManagerKpiDto>>(query.TrimEnd('&', '?'));
            return kpis ?? new List<ManagerKpiDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AnalyticsApiService] GetAllManagerKpiAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Get KPI for specific manager
    /// </summary>
    public async Task<ManagerKpiDto?> GetManagerKpiAsync(
        string userName,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        try
        {
            var query = $"/api/manager-kpi/{userName}?";
            if (startDate.HasValue)
                query += $"startDate={startDate.Value:yyyy-MM-dd}&";
            if (endDate.HasValue)
                query += $"endDate={endDate.Value:yyyy-MM-dd}";

            return await _apiService.GetAsync<ManagerKpiDto>(query.TrimEnd('&', '?'));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AnalyticsApiService] GetManagerKpiAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Get top managers ranking
    /// </summary>
    public async Task<List<ManagerKpiDto>> GetTopManagersAsync(int count = 10)
    {
        try
        {
            var query = $"/api/manager-kpi/top?count={count}";
            var kpis = await _apiService.GetAsync<List<ManagerKpiDto>>(query);
            return kpis ?? new List<ManagerKpiDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AnalyticsApiService] GetTopManagersAsync error: {ex}");
            throw;
        }
    }

    #endregion

    #region Commission Agents

    /// <summary>
    /// Get all commission agents (partners)
    /// </summary>
    public async Task<List<CommissionAgentDto>> GetCommissionAgentsAsync()
    {
        try
        {
            var agents = await _apiService.GetAsync<List<CommissionAgentDto>>("/api/commission/agents");
            return agents ?? new List<CommissionAgentDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AnalyticsApiService] GetCommissionAgentsAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Get commission agent stats
    /// </summary>
    public async Task<CommissionStatsDto?> GetCommissionStatsAsync(int agentId)
    {
        try
        {
            return await _apiService.GetAsync<CommissionStatsDto>($"/api/commission/agents/{agentId}/stats");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AnalyticsApiService] GetCommissionStatsAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Get commission transactions for agent
    /// </summary>
    public async Task<List<CommissionTransactionDto>> GetCommissionTransactionsAsync(
        int agentId,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        try
        {
            var query = $"/api/commission/agents/{agentId}/transactions?";
            if (startDate.HasValue)
                query += $"startDate={startDate.Value:yyyy-MM-dd}&";
            if (endDate.HasValue)
                query += $"endDate={endDate.Value:yyyy-MM-dd}";

            var transactions = await _apiService.GetAsync<List<CommissionTransactionDto>>(query.TrimEnd('&', '?'));
            return transactions ?? new List<CommissionTransactionDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AnalyticsApiService] GetCommissionTransactionsAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Get summary report for all commission agents
    /// </summary>
    public async Task<CommissionSummaryDto?> GetCommissionSummaryAsync()
    {
        try
        {
            return await _apiService.GetAsync<CommissionSummaryDto>("/api/commission/report");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AnalyticsApiService] GetCommissionSummaryAsync error: {ex}");
            throw;
        }
    }

    #endregion

    #region Commercial Analytics

    /// <summary>
    /// Get ABC analysis for products
    /// </summary>
    public async Task<List<AbcAnalysisDto>> GetAbcAnalysisAsync(int days = 90)
    {
        try
        {
            var query = $"/api/commercial-analytics/abc?days={days}";
            var analysis = await _apiService.GetAsync<List<AbcAnalysisDto>>(query);
            return analysis ?? new List<AbcAnalysisDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AnalyticsApiService] GetAbcAnalysisAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Get demand forecast for products
    /// </summary>
    public async Task<List<DemandForecastDto>> GetDemandForecastAsync(int forecastDays = 30)
    {
        try
        {
            var query = $"/api/commercial-analytics/forecast?forecastDays={forecastDays}";
            var forecast = await _apiService.GetAsync<List<DemandForecastDto>>(query);
            return forecast ?? new List<DemandForecastDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AnalyticsApiService] GetDemandForecastAsync error: {ex}");
            throw;
        }
    }

    #endregion
}

public class CommissionSummaryDto
{
    public int TotalAgents { get; set; }
    public decimal TotalBalance { get; set; }
    public decimal TotalAccrued { get; set; }
    public decimal TotalPaid { get; set; }
    public List<CommissionAgentDto> Agents { get; set; } = new();
}
