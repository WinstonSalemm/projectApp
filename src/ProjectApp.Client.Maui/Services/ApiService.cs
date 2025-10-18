using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProjectApp.Client.Maui.Services;

/// <summary>
/// Base API service for making HTTP requests to the backend
/// </summary>
public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly AuthService _authService;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiService(HttpClient httpClient, AuthService authService)
    {
        _httpClient = httpClient;
        _authService = authService;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Add JWT token to request headers
    /// </summary>
    private void AddAuthorizationHeader()
    {
        if (!string.IsNullOrEmpty(_authService.AccessToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _authService.AccessToken);
        }
    }

    /// <summary>
    /// GET request
    /// </summary>
    public async Task<T?> GetAsync<T>(string endpoint)
    {
        try
        {
            AddAuthorizationHeader();
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ApiService] GET {endpoint} failed: {ex.Message}");
            throw new ApiException($"Network error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ApiService] GET {endpoint} error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// POST request
    /// </summary>
    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
    {
        try
        {
            AddAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync(endpoint, data, _jsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ApiService] POST {endpoint} failed: {ex.Message}");
            throw new ApiException($"Network error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ApiService] POST {endpoint} error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// POST request without response body
    /// </summary>
    public async Task PostAsync<TRequest>(string endpoint, TRequest data)
    {
        try
        {
            AddAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync(endpoint, data, _jsonOptions);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ApiService] POST {endpoint} failed: {ex.Message}");
            throw new ApiException($"Network error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ApiService] POST {endpoint} error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// PUT request
    /// </summary>
    public async Task<TResponse?> PutAsync<TRequest, TResponse>(string endpoint, TRequest data)
    {
        try
        {
            AddAuthorizationHeader();
            var response = await _httpClient.PutAsJsonAsync(endpoint, data, _jsonOptions);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ApiService] PUT {endpoint} failed: {ex.Message}");
            throw new ApiException($"Network error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ApiService] PUT {endpoint} error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// DELETE request
    /// </summary>
    public async Task DeleteAsync(string endpoint)
    {
        try
        {
            AddAuthorizationHeader();
            var response = await _httpClient.DeleteAsync(endpoint);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ApiService] DELETE {endpoint} failed: {ex.Message}");
            throw new ApiException($"Network error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ApiService] DELETE {endpoint} error: {ex}");
            throw;
        }
    }
}

/// <summary>
/// Custom exception for API errors
/// </summary>
public class ApiException : Exception
{
    public ApiException(string message) : base(message) { }
    public ApiException(string message, Exception innerException) : base(message, innerException) { }
}
