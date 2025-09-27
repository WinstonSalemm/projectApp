using Microsoft.Extensions.Options;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Integrations.Telegram;

public interface IReturnsNotifier
{
    Task NotifyReturnAsync(Return ret, Sale sale, CancellationToken ct = default);
}

public class ReturnsNotifier(ITelegramService tg, IOptions<TelegramSettings> options, ILogger<ReturnsNotifier> logger) : IReturnsNotifier
{
    private readonly ITelegramService _tg = tg;
    private readonly TelegramSettings _settings = options.Value;
    private readonly ILogger<ReturnsNotifier> _logger = logger;

    public async Task NotifyReturnAsync(Return ret, Sale sale, CancellationToken ct = default)
    {
        try
        {
            var ids = _settings.ParseAllowedChatIds();
            if (ids.Count == 0) return;

            var itemsCount = ret.Items?.Count ?? sale.Items?.Count ?? 0;
            var qty = (ret.Items?.Sum(i => i.Qty)) ?? (sale.Items?.Sum(i => i.Qty) ?? 0m);
            var msg = $"Возврат #{ret.Id}\nПо продаже: #{sale.Id}\nДата: {ret.CreatedAt:yyyy-MM-dd HH:mm}\nКлиент: {(ret.ClientId ?? sale.ClientId)?.ToString() ?? sale.ClientName}\nПозиции: {itemsCount} (шт: {qty})\nСумма возврата: {ret.Sum}";

            foreach (var chatId in ids)
            {
                _ = await _tg.SendMessageAsync(chatId, msg, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ReturnsNotifier: failed to notify return {ReturnId}", ret.Id);
        }
    }
}
