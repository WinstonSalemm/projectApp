using Microsoft.Extensions.Options;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Integrations.Telegram;

public interface IDebtsNotifier
{
    Task NotifyDebtPaymentAsync(Debt debt, DebtPayment payment, CancellationToken ct = default);
}

public class DebtsNotifier(ITelegramService tg, IOptions<TelegramSettings> options, ILogger<DebtsNotifier> logger) : IDebtsNotifier
{
    private readonly ITelegramService _tg = tg;
    private readonly TelegramSettings _settings = options.Value;
    private readonly ILogger<DebtsNotifier> _logger = logger;

    public async Task NotifyDebtPaymentAsync(Debt debt, DebtPayment payment, CancellationToken ct = default)
    {
        try
        {
            var ids = _settings.ParseAllowedChatIds();
            if (ids.Count == 0) return;
            var msg = $"Оплата долга #{debt.Id}\nСумма платежа: {payment.Amount}\nОстаток: {debt.Amount}\nСтатус: {debt.Status}";
            foreach (var chatId in ids)
            {
                _ = await _tg.SendMessageAsync(chatId, msg, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DebtsNotifier: failed to notify payment for debt {DebtId}", debt.Id);
        }
    }
}
