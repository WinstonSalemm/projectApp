using System.Net.Http.Json;
using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.Services;

/// <summary>
/// Статус брака
/// </summary>
public enum DefectiveStatus
{
    Active = 0,
    Cancelled = 1
}

/// <summary>
/// DTO для брака
/// </summary>
public class DefectiveItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? Sku { get; set; }
    public int Quantity { get; set; }
    public int Warehouse { get; set; }  // 0 = ND40, 1 = IM40
    public string? Reason { get; set; }
    public DefectiveStatus Status { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string? CancelledBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}

public class CreateDefectiveDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public int Warehouse { get; set; } = 0;  // 0 = ND40
    public string? Reason { get; set; }
}

/// <summary>
/// API сервис для работы с браком
/// </summary>
public class DefectivesApiService
{
    private readonly HttpClient _httpClient;
    private readonly AuthService _authService;

    public DefectivesApiService(HttpClient httpClient, AuthService authService)
    {
        _httpClient = httpClient;
        _authService = authService;
    }

    /// <summary>
    /// Получить список брака
    /// </summary>
    public async Task<List<DefectiveItemDto>> GetDefectivesAsync(DateTime? from = null, DateTime? to = null)
    {
        try
        {
            var token = _authService.AccessToken;
            if (string.IsNullOrEmpty(token))
                return new List<DefectiveItemDto>();

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var query = "?";
            if (from.HasValue)
                query += $"from={from.Value:yyyy-MM-dd}&";
            if (to.HasValue)
                query += $"to={to.Value:yyyy-MM-dd}&";

            var response = await _httpClient.GetAsync($"api/defectives{query}");
            
            if (!response.IsSuccessStatusCode)
                return new List<DefectiveItemDto>();

            return await response.Content.ReadFromJsonAsync<List<DefectiveItemDto>>() 
                   ?? new List<DefectiveItemDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DefectivesApiService] GetDefectives error: {ex.Message}");
            return new List<DefectiveItemDto>();
        }
    }

    /// <summary>
    /// Создать списание брака
    /// </summary>
    public async Task<(bool Success, string? Error)> CreateDefectiveAsync(CreateDefectiveDto dto)
    {
        try
        {
            var token = _authService.AccessToken;
            if (string.IsNullOrEmpty(token))
                return (false, "Не авторизован");

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.PostAsJsonAsync("api/defectives", dto);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, error);
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DefectivesApiService] CreateDefective error: {ex.Message}");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Отменить списание брака
    /// </summary>
    public async Task<(bool Success, string? Error)> CancelDefectiveAsync(int id, string? reason)
    {
        try
        {
            var token = _authService.AccessToken;
            if (string.IsNullOrEmpty(token))
                return (false, "Не авторизован");

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var dto = new { Reason = reason };
            var response = await _httpClient.PostAsJsonAsync($"api/defectives/{id}/cancel", dto);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, error);
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DefectivesApiService] CancelDefective error: {ex.Message}");
            return (false, ex.Message);
        }
    }
}
