using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectApp.Api.Models;
using ProjectApp.Api.Data;

namespace ProjectApp.Api.Integrations.Telegram;

public interface IReturnsNotifier
{
    Task NotifyReturnAsync(Return ret, Sale sale, CancellationToken ct = default);
}

public class ReturnsNotifier(ITelegramService tg, IOptions<TelegramSettings> options, ILogger<ReturnsNotifier> logger, AppDbContext db) : IReturnsNotifier
{
    private readonly ITelegramService _tg = tg;
    private readonly TelegramSettings _settings = options.Value;
    private readonly ILogger<ReturnsNotifier> _logger = logger;
    private readonly AppDbContext _db = db;

    private static string HtmlEscape(string? s)
        => string.IsNullOrEmpty(s)
            ? string.Empty
            : s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    public async Task NotifyReturnAsync(Return ret, Sale sale, CancellationToken ct = default)
    {
        try
        {
            var ids = _settings.ParseAllowedChatIds();
            if (ids.Count == 0) return;

            // Timezone adjust
            var localTime = ret.CreatedAt.AddMinutes(_settings.TimeZoneOffsetMinutes);

            // Client string
            var clientName = string.IsNullOrWhiteSpace(sale.ClientName) ? "Посетитель" : sale.ClientName;
            var safeClient = HtmlEscape(clientName);

            // Prepare product names map
            var pids = sale.Items?.Select(i => i.ProductId).Distinct().ToList() ?? new List<int>();
            var prodMap = await _db.Products.AsNoTracking()
                .Where(p => pids.Contains(p.Id))
                .Select(p => new { p.Id, p.Name })
                .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

            // Build lines from return items
            var lines = new List<string>();
            int itemsCount = ret.Items?.Count ?? 0;
            decimal totalQty = 0m;
            decimal totalSum = 0m;
            foreach (var ri in ret.Items ?? new List<ReturnItem>())
            {
                var si = sale.Items.FirstOrDefault(x => x.Id == ri.SaleItemId);
                var pid = si?.ProductId ?? 0;
                prodMap.TryGetValue(pid, out var name);
                name ??= $"#{pid}";
                var sum = ri.Qty * ri.UnitPrice;
                totalQty += ri.Qty;
                totalSum += sum;
                var nameShort = name.Length > 28 ? name.Substring(0, 28) + "…" : name;
                var safeNameShort = HtmlEscape(nameShort);
                lines.Add($"{safeNameShort,-30} {ri.Qty,5:N0} x {ri.UnitPrice,9:N0} = {sum,10:N0}");
            }

            var title = $"<b>Возврат #{ret.Id}</b>";
            var header = $"По продаже: #{sale.Id}\nДата: {localTime:yyyy-MM-dd HH:mm}\nКлиент: {safeClient}\nПозиции: {itemsCount} (шт: {totalQty:N0})\nИтого: {totalSum:N0} сум";
            var itemsBlock = lines.Count > 0 ? ("\n<pre>" + string.Join("\n", lines) + "</pre>") : string.Empty;
            var msg = title + "\n" + header + itemsBlock;

            foreach (var chatId in ids)
            {
                _ = await _tg.SendMessageAsync(chatId, msg, "HTML", null, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ReturnsNotifier: failed to notify return {ReturnId}", ret.Id);
        }
    }
}
