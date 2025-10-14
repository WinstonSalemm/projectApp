using System;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace ProjectApp.Client.Maui.Services;

/// <summary>
/// Appends the bearer token from <see cref="AuthService"/> to outgoing API requests.
/// </summary>
public sealed class AuthHeaderHandler : DelegatingHandler
{
    private readonly AuthService _authService;
    private readonly AppSettings _settings;

    public AuthHeaderHandler(AuthService authService, AppSettings settings)
    {
        _authService = authService;
        _settings = settings;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            if (_authService.IsAuthenticated && request.Headers.Authorization is null)
            {
                if (ShouldAttachToken(request.RequestUri))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authService.AccessToken!);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AuthHeaderHandler] Failed to append bearer token: {ex}");
        }

        return base.SendAsync(request, cancellationToken);
    }

    private bool ShouldAttachToken(Uri? requestUri)
    {
        if (requestUri is null || !requestUri.IsAbsoluteUri)
        {
            // Relative URIs will use HttpClient.BaseAddress which should point to the API.
            return true;
        }

        if (!TryGetApiBaseUri(out var baseUri) || baseUri is null)
        {
            return false;
        }

        return string.Equals(requestUri.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
               && string.Equals(requestUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
               && requestUri.Port == baseUri.Port;
    }

    private bool TryGetApiBaseUri(out Uri? baseUri)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl)
            ? "http://localhost:5028"
            : _settings.ApiBaseUrl!;

        return Uri.TryCreate(baseUrl, UriKind.Absolute, out baseUri);
    }
}
