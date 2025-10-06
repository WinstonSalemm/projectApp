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

    private static string PaymentTypeRu(PaymentType pt) => pt switch
    {
        PaymentType.CashWithReceipt => "Наличные (чек)",
        PaymentType.CardWithReceipt => "Карта (чек)",
        PaymentType.ClickWithReceipt => "Click (чек)",
        PaymentType.CashNoReceipt => "Наличные (без чека)",
        PaymentType.ClickNoReceipt => "Click (без чека)",
        PaymentType.Click => "Click",
        PaymentType.Payme => "Payme",
        PaymentType.Site => "Сайт",
        PaymentType.Reservation => "Резервация",
        PaymentType.Return => "Возврат",
        PaymentType.Contract => "По договору",
        _ => pt.ToString()
    };

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
            var paymentRu = PaymentTypeRu(sale.PaymentType);
            var msg = $"{title}\nДата: {sale.CreatedAt:yyyy-MM-dd HH:mm}\nКлиент: {sale.ClientName}\nОплата: {paymentRu}\nПозиции: {itemsCount} (шт: {qty})\nИтого: {sale.Total}\nОператор: {sale.CreatedBy ?? "n/a"}";

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
