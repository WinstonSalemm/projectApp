using Microsoft.Extensions.Options;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Integrations.Telegram;

public interface ISalesNotifier
{
    Task NotifySaleAsync(Sale sale, CancellationToken ct = default);
}

public class SalesNotifier(ITelegramService tg, IOptions<TelegramSettings> options, ILogger<SalesNotifier> logger) : ISalesNotifier
{
    private readonly ITelegramService _tg = tg;
    private readonly TelegramSettings _settings = options.Value;
    private readonly ILogger<SalesNotifier> _logger = logger;

    public async Task NotifySaleAsync(Sale sale, CancellationToken ct = default)
    {
        try
        {
            var ids = _settings.ParseAllowedChatIds();
            if (ids.Count == 0)
            {
                _logger.LogInformation("SalesNotifier: no AllowedChatIds configured, skipping notification for sale {SaleId}", sale.Id);
                return;
            }

            var itemsCount = sale.Items?.Count ?? 0;
            var qty = sale.Items?.Sum(i => i.Qty) ?? 0m;
            var title = $"Продажа #{sale.Id}";
            var msg = $"{title}\nДата: {sale.CreatedAt:yyyy-MM-dd HH:mm}\nКлиент: {sale.ClientName}\nОплата: {sale.PaymentType}\nПозиции: {itemsCount} (шт: {qty})\nИтого: {sale.Total}\nОператор: {sale.CreatedBy ?? "n/a"}";

            foreach (var chatId in ids)
            {
                _ = await _tg.SendMessageAsync(chatId, msg, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SalesNotifier: failed to send notification for sale {SaleId}", sale.Id);
        }
    }
}
