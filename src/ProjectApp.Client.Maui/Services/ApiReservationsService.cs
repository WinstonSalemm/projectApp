using System.Net.Http.Headers;
using System.Net.Http.Json;

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
        var client = _httpClientFactory.CreateClient();
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
}
