using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Integrations.Telegram;

public interface ISalesNotifier
{
    Task NotifySaleAsync(Sale sale, CancellationToken ct = default);
}

public class SalesNotifier(ITelegramService tg, IOptions<TelegramSettings> options, ILogger<SalesNotifier> logger, AppDbContext db, IConfiguration config) : ISalesNotifier
{
    private readonly ITelegramService _tg = tg;
    private readonly TelegramSettings _settings = options.Value;
    private readonly ILogger<SalesNotifier> _logger = logger;
    private readonly AppDbContext _db = db;
    private readonly IConfiguration _config = config;

    private static string HtmlEscape(string? s)
        => string.IsNullOrEmpty(s)
            ? string.Empty
            : s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

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

            // Resolve timezone
            var localTime = sale.CreatedAt.AddMinutes(_settings.TimeZoneOffsetMinutes);

            // Resolve client name (default to "Посетитель")
            var clientName = string.IsNullOrWhiteSpace(sale.ClientName) ? "Посетитель" : sale.ClientName;

            // Resolve manager display name
            var createdBy = sale.CreatedBy ?? string.Empty;
            string managerDisplay = createdBy;
            try
            {
                if (!string.IsNullOrWhiteSpace(createdBy))
                {
                    var dbUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserName == createdBy, ct);
                    if (dbUser is not null && !string.IsNullOrWhiteSpace(dbUser.DisplayName))
                        managerDisplay = dbUser.DisplayName;
                    else
                    {
                        var cfgUsers = _config.GetSection("Users").Get<List<dynamic>>();
                        if (cfgUsers is not null)
                        {
                            foreach (var u in cfgUsers)
                            {
                                try
                                {
                                    string uname = u.UserName;
                                    if (string.Equals(uname, createdBy, StringComparison.OrdinalIgnoreCase))
                                    {
                                        managerDisplay = string.IsNullOrWhiteSpace((string?)u.DisplayName) ? uname : (string)u.DisplayName;
                                        break;
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch { }
            if (string.IsNullOrWhiteSpace(managerDisplay)) managerDisplay = "n/a";
            // Escape potentially unsafe parts
            var safeClient = HtmlEscape(clientName);
            var safeManager = HtmlEscape(managerDisplay);

            var itemsCount = sale.Items?.Count ?? 0;
            var qty = sale.Items?.Sum(i => i.Qty) ?? 0m;
            var paymentRu = PaymentTypeRu(sale.PaymentType);

            // Load product names for items
            var lines = new List<string>();
            try
            {
                var pids = sale.Items?.Select(i => i.ProductId).Distinct().ToList() ?? new List<int>();
                var prodMap = await _db.Products.AsNoTracking()
                    .Where(p => pids.Contains(p.Id))
                    .Select(p => new { p.Id, p.Sku, p.Name })
                    .ToDictionaryAsync(p => p.Id, p => p, ct);

                foreach (var it in sale.Items ?? new List<SaleItem>())
                {
                    prodMap.TryGetValue(it.ProductId, out var p);
                    var name = p?.Name ?? $"#{it.ProductId}";
                    var sum = it.Qty * it.UnitPrice;
                    // Trim name to keep lines compact
                    var nameShort = name.Length > 28 ? name.Substring(0, 28) + "…" : name;
                    var safeNameShort = HtmlEscape(nameShort);
                    lines.Add($"{safeNameShort,-30} {it.Qty,5:N0} x {it.UnitPrice,9:N0} = {sum,10:N0}");
                }
            }
            catch { }

            var title = $"<b>Продажа #{sale.Id}</b>";
            var header = $"Дата: {localTime:yyyy-MM-dd HH:mm}\nКлиент: {safeClient}\nОплата: {paymentRu}\nПозиции: {itemsCount} (шт: {qty:N0})\nИтого: {sale.Total:N0}\nМенеджер: {safeManager}";
            var itemsBlock = lines.Count > 0 ? ("\n<pre>" + string.Join("\n", lines) + "</pre>") : string.Empty;
            var msg = title + "\n" + header + itemsBlock;

            foreach (var chatId in ids)
            {
                _ = await _tg.SendMessageAsync(chatId, msg, "HTML", null, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SalesNotifier: failed to send notification for sale {SaleId}", sale.Id);
        }
    }
}
