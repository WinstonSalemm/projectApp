using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectApp.Api.Data;
using ProjectApp.Api.Integrations.Telegram;
using System.Text;
using System.IO;

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

            // Preload batch values to compute TotalValue = SUM(QtyRemaining * UnitCost)
            var productIds = stocks.Select(s => s.ProductId).ToArray();
            var batchAgg = await db.Batches.AsNoTracking()
                .Where(b => productIds.Contains(b.ProductId) && b.Qty > 0)
                .GroupBy(b => b.ProductId)
                .Select(g => new { ProductId = g.Key, TotalValue = g.Sum(x => x.Qty * x.UnitCost) })
                .ToDictionaryAsync(x => x.ProductId, x => x.TotalValue, ct);

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
                    TotalValue = batchAgg.TryGetValue(s.ProductId, out var tv) ? tv : 0m,
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

                // Экспортируем остатки в CSV (Excel-friendly)
                var prods = await db.Products.AsNoTracking()
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, ct);

                var sb = new StringBuilder();
                sb.AppendLine("ProductId,SKU,Name,ND_Qty,IM_Qty,Total_Qty,Total_Value");
                foreach (var s in stocks)
                {
                    var total = s.Nd + s.Im;
                    if (total <= 0) continue;
                    var sku = prods.TryGetValue(s.ProductId, out var p) ? (p.Sku ?? string.Empty) : string.Empty;
                    var name = prods.TryGetValue(s.ProductId, out var p2) ? (p2.Name ?? string.Empty) : string.Empty;
                    var totalValue = batchAgg.TryGetValue(s.ProductId, out var tv) ? tv : 0m;
                    string Esc(string x) => string.IsNullOrEmpty(x) ? string.Empty : (x.Contains(',') || x.Contains('"') ? $"\"{x.Replace("\"", "\"\"")}\"" : x);
                    sb.AppendLine(string.Join(',',
                        s.ProductId,
                        Esc(sku),
                        Esc(name),
                        s.Nd.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        s.Im.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        total.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        totalValue.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    ));
                }
                var csvBytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
                var fileName = $"stocks-{dateStr}.csv";

                // Отправим файл в Telegram
                foreach (var chatId in ids)
                {
                    try
                    {
                        await using var ms = new MemoryStream(csvBytes, writable: false);
                        ms.Position = 0;
                        await tg.SendDocumentAsync(chatId, ms, fileName, caption: $"Остатки на {dateStr}", ct);
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StockSnapshot: failed to save snapshot");
        }
    }
}
