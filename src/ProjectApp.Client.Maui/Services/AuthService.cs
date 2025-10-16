using System;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.Storage;

namespace ProjectApp.Client.Maui.Services;

public class AuthService
{
    private const string TokenKey = "Auth_AccessToken";
    private const string RoleKey = "Auth_Role";
    private const string UserNameKey = "Auth_UserName";
    private const string DisplayNameKey = "Auth_DisplayName";
    private const string ExpiresKey = "Auth_ExpiresAtUtc";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppSettings _settings;

    public string? AccessToken { get; private set; }
    public string? Role { get; private set; }
    public string? UserName { get; private set; }
    public string? DisplayName { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }
    public string? LastErrorMessage { get; private set; }

    public bool IsAuthenticated =>
        !string.IsNullOrWhiteSpace(AccessToken) &&
        ExpiresAtUtc is { } expires &&
        expires > DateTimeOffset.UtcNow.AddMinutes(1);

    public AuthService(IHttpClientFactory httpClientFactory, AppSettings settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        RestorePersisted();
    }

    private class LoginRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string? Password { get; set; }
    }

    private class LoginResponse
    {
        [JsonPropertyName("accessToken")] public string AccessToken { get; set; } = string.Empty;
        [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
        [JsonPropertyName("expiresAtUtc")] public DateTimeOffset ExpiresAtUtc { get; set; }
        [JsonPropertyName("userName")] public string UserName { get; set; } = string.Empty;
        [JsonPropertyName("displayName")] public string DisplayName { get; set; } = string.Empty;
    }

    public async Task<bool> LoginAsync(string userName, string? password, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
            var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl)
                ? "http://localhost:5028"
                : _settings.ApiBaseUrl!;
            client.BaseAddress = new Uri(baseUrl);

            var request = new LoginRequest
            {
                UserName = userName.Trim(),
                Password = string.IsNullOrWhiteSpace(password) ? null : password
            };

            var response = await client.PostAsJsonAsync("/api/auth/login", request, ct);
            if (!response.IsSuccessStatusCode)
            {
                string body = string.Empty;
                try
                {
                    body = await response.Content.ReadAsStringAsync(ct);
                }
                catch
                {
                    // ignore body read failures
                }

                LastErrorMessage = $"HTTP {(int)response.StatusCode} {response.StatusCode}. {Truncate(body, 300)}";
                return false;
            }

            var dto = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: ct);
            if (dto is null || string.IsNullOrWhiteSpace(dto.AccessToken))
            {
                LastErrorMessage = "Не удалось обработать ответ сервера авторизации.";
                return false;
            }

            AccessToken = dto.AccessToken;
            Role = dto.Role;
            UserName = dto.UserName;
            DisplayName = dto.DisplayName;
            ExpiresAtUtc = dto.ExpiresAtUtc;
            Persist();
            LastErrorMessage = null;
            return true;
        }
        catch
        {
            LastErrorMessage = "Сетевая ошибка при обращении к API (проверьте URL и подключение к интернету).";
            return false;
        }
    }

    public Task LogoutAsync()
    {
        ClearInMemory();
        ClearPersisted();
        return Task.CompletedTask;
    }

    public void LoginOffline(string userName, string displayName, string role)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new ArgumentException("User name must be provided for offline login.", nameof(userName));
        }

        AccessToken = "offline-token";
        Role = string.IsNullOrWhiteSpace(role) ? "Manager" : role;
        UserName = userName;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? userName : displayName;
        ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(12);
        LastErrorMessage = null;

        Persist();
    }

    public void ConfigureClient(HttpClient client)
    {
        if (!string.IsNullOrWhiteSpace(AccessToken))
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
        }
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= max ? value : value[..max] + "...";
    }

    private void Persist()
    {
        if (string.IsNullOrWhiteSpace(AccessToken))
        {
            ClearPersisted();
            return;
        }

        Preferences.Set(TokenKey, AccessToken);
        Preferences.Set(RoleKey, Role ?? string.Empty);
        Preferences.Set(UserNameKey, UserName ?? string.Empty);
        Preferences.Set(DisplayNameKey, DisplayName ?? string.Empty);

        if (ExpiresAtUtc.HasValue)
        {
            Preferences.Set(ExpiresKey, ExpiresAtUtc.Value.ToString("O"));
        }
        else
        {
            Preferences.Remove(ExpiresKey);
        }
    }

    private void RestorePersisted()
    {
        try
        {
            var token = Preferences.Get(TokenKey, null);
            if (string.IsNullOrWhiteSpace(token))
            {
                ClearInMemory();
                ClearPersisted();
                return;
            }

            AccessToken = token;
            Role = Preferences.Get(RoleKey, null);
            UserName = Preferences.Get(UserNameKey, null);
            DisplayName = Preferences.Get(DisplayNameKey, null);

            var expiresRaw = Preferences.Get(ExpiresKey, null);
            if (DateTimeOffset.TryParse(expiresRaw, out var expires))
            {
                ExpiresAtUtc = expires;
            }
            else
            {
                ExpiresAtUtc = null;
            }

            if (!IsAuthenticated)
            {
                ClearInMemory();
                ClearPersisted();
            }
        }
        catch
        {
            ClearInMemory();
        }
    }

    private void ClearInMemory()
    {
        AccessToken = null;
        Role = null;
        UserName = null;
        DisplayName = null;
        ExpiresAtUtc = null;
        LastErrorMessage = null;
    }

    private void ClearPersisted()
    {
        Preferences.Remove(TokenKey);
        Preferences.Remove(RoleKey);
        Preferences.Remove(UserNameKey);
        Preferences.Remove(DisplayNameKey);
        Preferences.Remove(ExpiresKey);
    }

    public void Logout()
    {
        ClearPersisted();
        ClearInMemory();
    }
}
