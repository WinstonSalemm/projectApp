using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectApp.Client.Maui.Models.Dtos;

namespace ProjectApp.Client.Maui.Services;

/// <summary>
/// API Service for debts and debtors management
/// </summary>
public class DebtorsApiService
{
    private readonly ApiService _apiService;

    public DebtorsApiService(ApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Get list of all debtors
    /// </summary>
    public async Task<List<DebtorDto>> GetDebtorsAsync()
    {
        try
        {
            var debtors = await _apiService.GetAsync<List<DebtorDto>>("/api/clients/debtors");
            return debtors ?? new List<DebtorDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DebtorsApiService] GetDebtorsAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Get debt details by ID
    /// </summary>
    public async Task<DebtDetailsDto?> GetDebtDetailsAsync(int debtId)
    {
        try
        {
            return await _apiService.GetAsync<DebtDetailsDto>($"/api/debts/{debtId}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DebtorsApiService] GetDebtDetailsAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Get all debts for a specific client
    /// </summary>
    public async Task<List<DebtDetailsDto>> GetClientDebtsAsync(int clientId)
    {
        try
        {
            var debts = await _apiService.GetAsync<List<DebtDetailsDto>>($"/api/debts/by-client/{clientId}");
            return debts ?? new List<DebtDetailsDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DebtorsApiService] GetClientDebtsAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Pay debt (partial or full payment)
    /// </summary>
    public async Task PayDebtAsync(int debtId, PayDebtRequest request)
    {
        try
        {
            await _apiService.PostAsync($"/api/debts/{debtId}/pay", request);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DebtorsApiService] PayDebtAsync error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Get payment history for a debt
    /// </summary>
    public async Task<List<DebtPaymentDto>> GetDebtPaymentsAsync(int debtId)
    {
        try
        {
            var payments = await _apiService.GetAsync<List<DebtPaymentDto>>($"/api/debts/{debtId}/payments");
            return payments ?? new List<DebtPaymentDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DebtorsApiService] GetDebtPaymentsAsync error: {ex}");
            throw;
        }
    }
}

public class DebtPaymentDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public string? Notes { get; set; }
    public string? CreatedBy { get; set; }
}
