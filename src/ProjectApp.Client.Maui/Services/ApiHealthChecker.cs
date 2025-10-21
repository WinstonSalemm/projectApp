using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace ProjectApp.Client.Maui.Services;

/// <summary>
/// Проверка доступности API при старте приложения
/// </summary>
public class ApiHealthChecker
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppSettings _settings;
    private readonly ILogger<ApiHealthChecker> _logger;

    public ApiHealthChecker(
        IHttpClientFactory httpClientFactory,
        AppSettings settings,
        ILogger<ApiHealthChecker> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Проверить доступность API
    /// </summary>
    /// <param name="timeoutSeconds">Таймаут проверки в секундах</param>
    /// <returns>True если API доступен</returns>
    public async Task<ApiHealthResult> CheckHealthAsync(int timeoutSeconds = 5)
    {
        try
        {
            var apiUrl = _settings.ApiBaseUrl ?? "https://tranquil-upliftment-production.up.railway.app";
            _logger.LogInformation("🔍 Проверка доступности API: {Url}", apiUrl);

            var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
            client.BaseAddress = new Uri(apiUrl);
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            // Пробуем подключиться к существующему endpoint (products - всегда доступен)
            var response = await client.GetAsync("/api/products?pageSize=1");
            
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogInformation("✅ API доступен: {Url}", apiUrl);
                return new ApiHealthResult
                {
                    IsAvailable = true,
                    ApiUrl = apiUrl,
                    Message = "API доступен"
                };
            }
            else
            {
                _logger.LogWarning("⚠️ API вернул ошибку: {StatusCode}", response.StatusCode);
                return new ApiHealthResult
                {
                    IsAvailable = false,
                    ApiUrl = apiUrl,
                    Message = $"API недоступен (HTTP {response.StatusCode})"
                };
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("⏱️ Таймаут подключения к API");
            return new ApiHealthResult
            {
                IsAvailable = false,
                ApiUrl = _settings.ApiBaseUrl ?? "unknown",
                Message = "Таймаут подключения"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "🌐 Ошибка подключения к API");
            return new ApiHealthResult
            {
                IsAvailable = false,
                ApiUrl = _settings.ApiBaseUrl ?? "unknown",
                Message = "Нет интернета или API недоступен"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Неожиданная ошибка при проверке API");
            return new ApiHealthResult
            {
                IsAvailable = false,
                ApiUrl = _settings.ApiBaseUrl ?? "unknown",
                Message = $"Ошибка: {ex.Message}"
            };
        }
    }
}

public class ApiHealthResult
{
    public bool IsAvailable { get; set; }
    public string ApiUrl { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
