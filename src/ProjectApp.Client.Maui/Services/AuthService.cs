using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.Storage;

namespace ProjectApp.Client.Maui.Services;

    public class AuthService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppSettings _settings;

    public string? AccessToken { get; private set; }
    public string? Role { get; private set; }
    public string? UserName { get; private set; }
    public string? DisplayName { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }
    public string? LastErrorMessage { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken) && ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1);

    public AuthService(IHttpClientFactory httpClientFactory, AppSettings settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        // Load from preferences
        AccessToken = Preferences.Get("Auth_AccessToken", (string?)null);
        Role = Preferences.Get("Auth_Role", (string?)null);
        UserName = Preferences.Get("Auth_UserName", (string?)null);
        DisplayName = Preferences.Get("Auth_DisplayName", (string?)null);
        var expStr = Preferences.Get("Auth_ExpiresAtUtc", (string?)null);
        if (DateTimeOffset.TryParse(expStr, out var exp)) ExpiresAtUtc = exp;
        // Drop expired token
        if (!IsAuthenticated) ClearPersisted();
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
            var client = _httpClientFactory.CreateClient();
            var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
            client.BaseAddress = new Uri(baseUrl);
            var req = new LoginRequest { UserName = userName.Trim(), Password = string.IsNullOrWhiteSpace(password) ? null : password };
            var resp = await client.PostAsJsonAsync("/api/auth/login", req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                string body = string.Empty;
                try { body = await resp.Content.ReadAsStringAsync(ct); } catch { }
                LastErrorMessage = $"HTTP {(int)resp.StatusCode} {resp.StatusCode}. {Truncate(body, 300)}";
                return false;
            }
            var dto = await resp.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: ct);
            if (dto is null || string.IsNullOrWhiteSpace(dto.AccessToken)) return false;
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
            LastErrorMessage = "Сетевая ошибка при обращении к API (проверьте URL и интернет).";
            return false;
        }
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Length <= max ? s : s.Substring(0, max) + "...";
    }

    public Task LogoutAsync()
    {
        AccessToken = null;
        Role = null;
        UserName = null;
        DisplayName = null;
        ExpiresAtUtc = null;
        ClearPersisted();
        return Task.CompletedTask;
    }

    private void Persist()
    {
        Preferences.Set("Auth_AccessToken", AccessToken);
        Preferences.Set("Auth_Role", Role);
        Preferences.Set("Auth_UserName", UserName);
        Preferences.Set("Auth_DisplayName", DisplayName);
        Preferences.Set("Auth_ExpiresAtUtc", ExpiresAtUtc?.ToString("o"));
    }

    private void ClearPersisted()
    {
        Preferences.Remove("Auth_AccessToken");
        Preferences.Remove("Auth_Role");
        Preferences.Remove("Auth_UserName");
        Preferences.Remove("Auth_DisplayName");
        Preferences.Remove("Auth_ExpiresAtUtc");
    }

    public void ConfigureClient(HttpClient client)
    {
        if (!string.IsNullOrWhiteSpace(AccessToken))
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
        }
    }
}
