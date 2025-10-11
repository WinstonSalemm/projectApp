using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectApp.Api.Data;
using ProjectApp.Api.Integrations.Telegram;

namespace ProjectApp.Api.Services;

public class StockSnapshotHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StockSnapshotHostedService> _logger;
    private readonly TelegramSettings _tgSettings;

    public StockSnapshotHostedService(IServiceScopeFactory scopeFactory, IOptions<TelegramSettings> tg, ILogger<StockSnapshotHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _tgSettings = tg.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nowUtc = DateTime.UtcNow;
                var offset = TimeSpan.FromMinutes(_tgSettings.TimeZoneOffsetMinutes);
                var nowLocal = nowUtc + offset;
                // target 23:00 local today/next
                var todayLocal = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 23, 0, 0, DateTimeKind.Unspecified);
                var targetUtc = todayLocal - offset;
                if (targetUtc <= nowUtc) targetUtc = todayLocal.AddDays(1) - offset;
                var delay = targetUtc - nowUtc;
                _logger.LogInformation("StockSnapshot: sleeping for {Delay} until {TargetUtc}", delay, targetUtc);
                await Task.Delay(delay, stoppingToken);

                await TakeSnapshotAsync(stoppingToken);
            }
            catch (TaskCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StockSnapshot: error in scheduler loop");
                try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch { }
            }
        }
    }

    private async Task TakeSnapshotAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tg = scope.ServiceProvider.GetRequiredService<ITelegramService>();

            var stocks = await db.Stocks.AsNoTracking()
                .GroupBy(s => s.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    Nd = g.Where(x => x.Register == Models.StockRegister.ND40).Sum(x => x.Qty),
                    Im = g.Where(x => x.Register == Models.StockRegister.IM40).Sum(x => x.Qty)
                })
                .ToListAsync(ct);

            var createdAt = DateTime.UtcNow;
            int rows = 0;
            foreach (var s in stocks)
            {
                var total = s.Nd + s.Im;
                if (total <= 0) continue; // автоисключение нулевых позиций
                db.StockSnapshots.Add(new Models.StockSnapshot
                {
                    ProductId = s.ProductId,
                    NdQty = s.Nd,
                    ImQty = s.Im,
                    TotalQty = total,
                    CreatedAt = createdAt
                });
                rows++;
            }
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("StockSnapshot: saved {Rows} rows at {Ts}", rows, createdAt);

            var ids = _tgSettings.ParseAllowedChatIds();
            if (ids.Count > 0)
            {
                var dateStr = (createdAt + TimeSpan.FromMinutes(_tgSettings.TimeZoneOffsetMinutes)).ToString("yyyy-MM-dd");
                var msg = $"Суточный снимок остатков сохранен за {dateStr}: строк {rows}";
                foreach (var chatId in ids)
                {
                    try { await tg.SendMessageAsync(chatId, msg, ct); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StockSnapshot: failed to save snapshot");
        }
    }
}
