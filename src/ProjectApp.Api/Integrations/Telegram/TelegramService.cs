using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace ProjectApp.Api.Integrations.Telegram;

public interface ITelegramService
{
    Task<bool> SendMessageAsync(long chatId, string text, CancellationToken ct = default);
    Task<bool> SendMessageAsync(long chatId, string text, object? replyMarkup, CancellationToken ct = default);
    Task<bool> SendMessageAsync(long chatId, string text, string? parseMode, object? replyMarkup, CancellationToken ct = default);
    Task<bool> SendPhotoAsync(long chatId, Stream fileStream, string fileName, string? caption = null, string? parseMode = null, CancellationToken ct = default);
    Task<bool> SetWebhookAsync(string url, string? secretToken, CancellationToken ct = default);
    Task<bool> DeleteWebhookAsync(CancellationToken ct = default);
    Task<string> GetWebhookInfoAsync(CancellationToken ct = default);
    Task<(bool ok, string body, int status)> SendMessageDebugAsync(long chatId, string text, object? replyMarkup, CancellationToken ct = default);
}

public class TelegramService(IHttpClientFactory httpClientFactory, IOptions<TelegramSettings> options) : ITelegramService
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly TelegramSettings _settings = options.Value;

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("telegram");
        client.BaseAddress = new Uri($"https://api.telegram.org/bot{_settings.BotToken}/");
        return client;
    }

    public async Task<bool> SendMessageAsync(long chatId, string text, CancellationToken ct = default)
    {
        return await SendMessageAsync(chatId, text, (string?)null, null, ct);
    }

    public async Task<bool> SendMessageAsync(long chatId, string text, object? replyMarkup, CancellationToken ct = default)
    {
        return await SendMessageAsync(chatId, text, null, replyMarkup, ct);
    }

    public async Task<bool> SendMessageAsync(long chatId, string text, string? parseMode, object? replyMarkup, CancellationToken ct = default)
    {
        var client = CreateClient();
        var payload = new Dictionary<string, object?>
        {
            ["chat_id"] = chatId,
            ["text"] = text
        };
        if (!string.IsNullOrWhiteSpace(parseMode)) payload["parse_mode"] = parseMode;
        if (replyMarkup is not null) payload["reply_markup"] = replyMarkup;
        var resp = await client.PostAsJsonAsync("sendMessage", payload, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> SendPhotoAsync(long chatId, Stream fileStream, string fileName, string? caption = null, string? parseMode = null, CancellationToken ct = default)
    {
        var client = CreateClient();
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "photo", fileName);
        content.Add(new StringContent(chatId.ToString()), "chat_id");
        if (!string.IsNullOrWhiteSpace(caption)) content.Add(new StringContent(caption), "caption");
        if (!string.IsNullOrWhiteSpace(parseMode)) content.Add(new StringContent(parseMode), "parse_mode");
        var resp = await client.PostAsync("sendPhoto", content, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> SetWebhookAsync(string url, string? secretToken, CancellationToken ct = default)
    {
        var client = CreateClient();
        var payload = new Dictionary<string, string>
        {
            ["url"] = url
        };
        if (!string.IsNullOrWhiteSpace(secretToken)) payload["secret_token"] = secretToken!;
        var resp = await client.PostAsync("setWebhook", new FormUrlEncodedContent(payload), ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteWebhookAsync(CancellationToken ct = default)
    {
        var client = CreateClient();
        var resp = await client.PostAsync("deleteWebhook", new FormUrlEncodedContent([]), ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<string> GetWebhookInfoAsync(CancellationToken ct = default)
    {
        var client = CreateClient();
        var resp = await client.GetAsync("getWebhookInfo", ct);
        var content = await resp.Content.ReadAsStringAsync(ct);
        return content;
    }

    public async Task<(bool ok, string body, int status)> SendMessageDebugAsync(long chatId, string text, object? replyMarkup, CancellationToken ct = default)
    {
        var client = CreateClient();
        var payload = new Dictionary<string, object?>
        {
            ["chat_id"] = chatId,
            ["text"] = text
        };
        if (replyMarkup is not null) payload["reply_markup"] = replyMarkup;
        var resp = await client.PostAsJsonAsync("sendMessage", payload, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        return (resp.IsSuccessStatusCode, body, (int)resp.StatusCode);
    }
}
