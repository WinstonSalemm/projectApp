using System.Net.Http.Json;
using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.Services;

/// <summary>
/// DTO для сводки инкассации
/// </summary>
public class CashCollectionSummaryDto
{
    public decimal CurrentAccumulated { get; set; }
    public DateTime? LastCollectionDate { get; set; }
    public decimal TotalRemainingAmount { get; set; }
    public List<CashCollectionDto> History { get; set; } = new();
}

public class CashCollectionDto
{
    public int Id { get; set; }
    public DateTime CollectionDate { get; set; }
    public decimal AccumulatedAmount { get; set; }
    public decimal CollectedAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string? Notes { get; set; }
    public string? CreatedBy { get; set; }
}

public class CreateCashCollectionDto
{
    public decimal CollectedAmount { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// API сервис для работы с инкассациями
/// </summary>
public class CashCollectionApiService
{
    private readonly HttpClient _httpClient;
    private readonly AuthService _authService;

    public CashCollectionApiService(HttpClient httpClient, AuthService authService)
    {
        _httpClient = httpClient;
        _authService = authService;
    }

    /// <summary>
    /// Получить сводку
    /// </summary>
    public async Task<CashCollectionSummaryDto?> GetSummaryAsync()
    {
        try
        {
            var token = _authService.AccessToken;
            if (string.IsNullOrEmpty(token))
                return null;

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync("api/cash-collection/summary");
            
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<CashCollectionSummaryDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CashCollectionApiService] GetSummary error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Провести инкассацию
    /// </summary>
    public async Task<(bool Success, string? Error)> CreateCollectionAsync(decimal collectedAmount, string? notes)
    {
        try
        {
            var token = _authService.AccessToken;
            if (string.IsNullOrEmpty(token))
                return (false, "Не авторизован");

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var dto = new CreateCashCollectionDto
            {
                CollectedAmount = collectedAmount,
                Notes = notes
            };

            var response = await _httpClient.PostAsJsonAsync("api/cash-collection", dto);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, error);
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CashCollectionApiService] CreateCollection error: {ex.Message}");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Удалить последнюю инкассацию
    /// </summary>
    public async Task<(bool Success, string? Error)> DeleteLastCollectionAsync()
    {
        try
        {
            var token = _authService.AccessToken;
            if (string.IsNullOrEmpty(token))
                return (false, "Не авторизован");

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.DeleteAsync("api/cash-collection/last");
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, error);
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CashCollectionApiService] DeleteLast error: {ex.Message}");
            return (false, ex.Message);
        }
    }
}
