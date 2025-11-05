using System.Net.Http.Headers;
using System.Net.Http.Json;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.Services;

public class ApiReservationsService : IReservationsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppSettings _settings;
    private readonly AuthService _auth;

    public ApiReservationsService(IHttpClientFactory httpClientFactory, AppSettings settings, AuthService auth)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _auth = auth;
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.Api);
        var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiBaseUrl) ? "http://localhost:5028" : _settings.ApiBaseUrl!;
        client.BaseAddress = new Uri(baseUrl);
        _auth.ConfigureClient(client);
        return client;
    }

    public async Task<int?> CreateReservationAsync(ReservationCreateDraft draft, bool waitForPhoto, string source, CancellationToken ct = default)
    {
        var client = CreateClient();
        var dto = new
        {
            clientId = draft.ClientId,
            paid = draft.Paid,
            note = draft.Note,
            waitForPhoto = waitForPhoto,
            source = source,
            items = draft.Items.Select(i => new { productId = i.ProductId, register = i.Register.ToString(), qty = i.Qty }).ToList()
        };
        var resp = await client.PostAsJsonAsync("/api/reservations", dto, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var view = await resp.Content.ReadFromJsonAsync<ReservationCreateResponse>(cancellationToken: ct);
        return view?.Id;
    }

    private class ReservationCreateResponse
    {
        public int Id { get; set; }
    }

    public async Task<bool> UploadReservationPhotoAsync(int reservationId, Stream photoStream, string fileName, CancellationToken ct = default)
    {
        var client = CreateClient();
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(photoStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "file", fileName);
        var resp = await client.PostAsync($"/api/reservations/{reservationId}/photo", content, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<ReservationListItem>> GetReservationsAsync(string? status = null, int? clientId = null, bool? mine = null, CancellationToken ct = default)
    {
        var client = CreateClient();
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(status)) qs.Add($"status={Uri.EscapeDataString(status)}");
        if (clientId.HasValue) qs.Add($"clientId={clientId.Value}");
        if (mine.HasValue) qs.Add($"mine={(mine.Value ? "true" : "false")}");
        var url = "/api/reservations" + (qs.Count > 0 ? ("?" + string.Join("&", qs)) : string.Empty);
        var list = await client.GetFromJsonAsync<List<ReservationListItem>>(url, ct);
        return list ?? new List<ReservationListItem>();
    }

    public async Task<ReservationDetailsDto?> GetReservationAsync(int id, CancellationToken ct = default)
    {
        var client = CreateClient();
        var dto = await client.GetFromJsonAsync<ReservationDetailsDto>($"/api/reservations/{id}", ct);
        return dto;
    }

    public async Task<bool> PayAsync(int reservationId, decimal amount, ReservationPaymentMethod method, string? note = null, CancellationToken ct = default)
    {
        var client = CreateClient();
        var body = new { amount = amount, method = (int)method, note = note };
        var resp = await client.PatchAsJsonAsync($"/api/reservations/{reservationId}/pay", body, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<ReservationAlertClientDto>> GetAlertsAsync(DateTime? sinceUtc = null, CancellationToken ct = default)
    {
        var client = CreateClient();
        string url;
        if (sinceUtc.HasValue)
        {
            // Use round-trip format for UTC
            var since = sinceUtc.Value.ToUniversalTime().ToString("o");
            url = $"/api/reservations/alerts?since={Uri.EscapeDataString(since)}";
        }
        else
        {
            url = "/api/reservations/alerts";
        }
        var list = await client.GetFromJsonAsync<List<ReservationAlertClientDto>>(url, ct);
        return list ?? new List<ReservationAlertClientDto>();
    }
}

