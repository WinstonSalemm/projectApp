using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectApp.Api.Data;

namespace ProjectApp.Api.Integrations.Telegram;

public class DailySummaryHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailySummaryHostedService> _logger;
    private readonly TelegramSettings _settings;

    public DailySummaryHostedService(IServiceScopeFactory scopeFactory, IOptions<TelegramSettings> options, ILogger<DailySummaryHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nowUtc = DateTime.UtcNow;
                var offset = TimeSpan.FromMinutes(_settings.TimeZoneOffsetMinutes);
                var nowLocal = nowUtc + offset;

                // Target time today at 23:00 local
                var todayLocal = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 23, 0, 0, DateTimeKind.Unspecified);
                var targetUtc = todayLocal - offset;
                if (targetUtc <= nowUtc)
                {
                    // schedule for next day
                    var tomorrowLocal = todayLocal.AddDays(1);
                    targetUtc = tomorrowLocal - offset;
                }

                var delay = targetUtc - nowUtc;
                _logger.LogInformation("DailySummary: sleeping for {Delay} until {TargetUtc}", delay, targetUtc);
                await Task.Delay(delay, stoppingToken);

                await SendSummaryAsync(stoppingToken);

                // after run, schedule explicitly for next day 23:00 local
                // compute next target again to be robust
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DailySummary: error in scheduler loop");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task SendSummaryAsync(CancellationToken ct)
    {
        try
        {
            var ids = _settings.ParseAllowedChatIds();
            if (ids.Count == 0)
            {
                _logger.LogInformation("DailySummary: AllowedChatIds empty, skipping");
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tg = scope.ServiceProvider.GetRequiredService<ITelegramService>();

            var offset = TimeSpan.FromMinutes(_settings.TimeZoneOffsetMinutes);
            var nowUtc = DateTime.UtcNow;
            var localToday = (nowUtc + offset).Date; // 00:00 local
            var fromUtc = localToday - offset;
            var toUtc = localToday.AddDays(1) - offset;

            var rows = await db.Sales
                .AsNoTracking()
                .Where(s => s.CreatedAt >= fromUtc && s.CreatedAt < toUtc)
                .Select(s => new { s.Total, s.CreatedBy, Qty = s.Items.Sum(i => i.Qty) })
                .ToListAsync(ct);

            var totalAmount = rows.Sum(r => r.Total);
            var totalQty = rows.Sum(r => r.Qty);
            var salesCount = rows.Count;
            var top = rows
                .GroupBy(r => r.CreatedBy ?? "unknown")
                .Select(g => new { Seller = g.Key, Amount = g.Sum(x => x.Total) })
                .OrderByDescending(x => x.Amount)
                .FirstOrDefault();

            var periodStr = localToday.ToString("yyyy-MM-dd");
            var msg = $"Ежедневная сводка за {periodStr}\nОборот: {totalAmount}\nШтук: {totalQty}\nЧеки: {salesCount}\nТоп продавец: {top?.Seller ?? "нет"} ({top?.Amount ?? 0m})";

            foreach (var chatId in ids)
            {
                _ = await tg.SendMessageAsync(chatId, msg, ct);
            }
            _logger.LogInformation("DailySummary: sent summary for {Date}", periodStr);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DailySummary: failed to send summary");
        }
    }
}
