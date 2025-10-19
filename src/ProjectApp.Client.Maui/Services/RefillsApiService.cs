using System.Net.Http.Json;
using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.Services;

/// <summary>
/// Статус перезарядки
/// </summary>
public enum RefillStatus
{
    Active = 0,
    Cancelled = 1
}

/// <summary>
/// DTO для перезарядки
/// </summary>
public class RefillOperationDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? Sku { get; set; }
    public int Quantity { get; set; }
    public int Warehouse { get; set; }  // 0 = ND40, 1 = IM40
    public decimal CostPerUnit { get; set; }
    public decimal TotalCost { get; set; }
    public string? Notes { get; set; }
    public RefillStatus Status { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string? CancelledBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}

public class CreateRefillDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public int Warehouse { get; set; } = 0;  // 0 = ND40
    public decimal CostPerUnit { get; set; }
    public string? Notes { get; set; }
}

public class RefillStatsDto
{
    public int TotalRefills { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalCost { get; set; }
    public decimal AverageCostPerUnit { get; set; }
}

/// <summary>
/// API сервис для работы с перезарядками
/// </summary>
public class RefillsApiService
{
    private readonly HttpClient _httpClient;
    private readonly AuthService _authService;

    public RefillsApiService(HttpClient httpClient, AuthService authService)
    {
        _httpClient = httpClient;
        _authService = authService;
    }

    /// <summary>
    /// Получить список перезарядок
    /// </summary>
    public async Task<List<RefillOperationDto>> GetRefillsAsync(DateTime? from = null, DateTime? to = null)
    {
        try
        {
            var token = _authService.AccessToken;
            if (string.IsNullOrEmpty(token))
                return new List<RefillOperationDto>();

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var query = "?";
            if (from.HasValue)
                query += $"from={from.Value:yyyy-MM-dd}&";
            if (to.HasValue)
                query += $"to={to.Value:yyyy-MM-dd}&";

            var response = await _httpClient.GetAsync($"api/refills{query}");
            
            if (!response.IsSuccessStatusCode)
                return new List<RefillOperationDto>();

            return await response.Content.ReadFromJsonAsync<List<RefillOperationDto>>() 
                   ?? new List<RefillOperationDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RefillsApiService] GetRefills error: {ex.Message}");
            return new List<RefillOperationDto>();
        }
    }

    /// <summary>
    /// Создать перезарядку
    /// </summary>
    public async Task<(bool Success, string? Error)> CreateRefillAsync(CreateRefillDto dto)
    {
        try
        {
            var token = _authService.AccessToken;
            if (string.IsNullOrEmpty(token))
                return (false, "Не авторизован");

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.PostAsJsonAsync("api/refills", dto);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, error);
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RefillsApiService] CreateRefill error: {ex.Message}");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Отменить перезарядку
    /// </summary>
    public async Task<(bool Success, string? Error)> CancelRefillAsync(int id, string? reason)
    {
        try
        {
            var token = _authService.AccessToken;
            if (string.IsNullOrEmpty(token))
                return (false, "Не авторизован");

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var dto = new { Reason = reason };
            var response = await _httpClient.PostAsJsonAsync($"api/refills/{id}/cancel", dto);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, error);
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RefillsApiService] CancelRefill error: {ex.Message}");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Получить статистику
    /// </summary>
    public async Task<RefillStatsDto?> GetStatsAsync(DateTime? from = null, DateTime? to = null)
    {
        try
        {
            var token = _authService.AccessToken;
            if (string.IsNullOrEmpty(token))
                return null;

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var query = "?";
            if (from.HasValue)
                query += $"from={from.Value:yyyy-MM-dd}&";
            if (to.HasValue)
                query += $"to={to.Value:yyyy-MM-dd}&";

            var response = await _httpClient.GetAsync($"api/refills/stats{query}");
            
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<RefillStatsDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RefillsApiService] GetStats error: {ex.Message}");
            return null;
        }
    }
}
