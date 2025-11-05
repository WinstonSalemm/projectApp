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

            // Продажи за сутки
            var sales = await db.Sales
                .AsNoTracking()
                .Where(s => s.CreatedAt >= fromUtc && s.CreatedAt < toUtc)
                .Include(s => s.Items)
                .ToListAsync(ct);

            var totalAmount = sales.Sum(s => s.Total);
            var totalQty = sales.SelectMany(s => s.Items).Sum(i => i.Qty);
            var salesCount = sales.Count;
            var top = sales
                .GroupBy(r => r.CreatedBy ?? "unknown")
                .Select(g => new { Seller = g.Key, Amount = g.Sum(x => x.Total) })
                .OrderByDescending(x => x.Amount)
                .FirstOrDefault();

            // Продажи по позициям (агрегация по товару)
            var itemsAgg = sales.SelectMany(s => s.Items)
                .GroupBy(i => new { i.ProductId, Name = i.ProductName ?? $"Product #{i.ProductId}", Sku = i.Sku ?? string.Empty })
                .Select(g => new
                {
                    g.Key.ProductId,
                    g.Key.Sku,
                    g.Key.Name,
                    Qty = g.Sum(x => x.Qty),
                    Revenue = g.Sum(x => x.UnitPrice * x.Qty),
                    AvgPrice = g.Sum(x => x.UnitPrice * x.Qty) / (g.Sum(x => x.Qty) == 0 ? 1 : g.Sum(x => x.Qty))
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();

            // Возвраты за сутки
            var returns = await db.Returns
                .AsNoTracking()
                .Include(r => r.Items)
                .Where(r => r.CreatedAt >= fromUtc && r.CreatedAt < toUtc)
                .ToListAsync(ct);
            var returnsCount = returns.Count;
            var returnsSum = returns.Sum(r => r.Sum);

            // Долги, выданные за сутки (созданные долги)
            var debts = await db.Debts
                .AsNoTracking()
                .Where(d => d.CreatedAt >= fromUtc && d.CreatedAt < toUtc)
                .ToListAsync(ct);
            var debtsCount = debts.Count;
            var debtsSum = debts.Sum(d => d.OriginalAmount);

            // Договоры: новые и старые
            var newContracts = await db.Contracts
                .AsNoTracking()
                .Include(c => c.Items)
                .Where(c => c.CreatedAt >= fromUtc && c.CreatedAt < toUtc)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync(ct);
            var newContractsCount = newContracts.Count;
            var oldContractsUsedCount = await (from d in db.ContractDeliveries.AsNoTracking()
                                               join c in db.Contracts.AsNoTracking() on d.ContractId equals c.Id
                                               where d.DeliveredAt >= fromUtc && d.DeliveredAt < toUtc && c.CreatedAt < fromUtc
                                               select d.ContractId).Distinct().CountAsync(ct);

            var periodStr = localToday.ToString("yyyy-MM-dd");
            var msgSb = new System.Text.StringBuilder();
            msgSb.AppendLine($"📅 Отчет за {periodStr}");
            msgSb.AppendLine($"💰 Оборот: {totalAmount:N0} UZS");
            msgSb.AppendLine($"🧾 Чеки: {salesCount}, Штук: {totalQty:N0}");
            if (top != null) msgSb.AppendLine($"🏅 Топ продавец: {top.Seller} ({top.Amount:N0} UZS)");
            msgSb.AppendLine();
            msgSb.AppendLine("📦 Продажи по позициям:");
            int line = 0;
            foreach (var it in itemsAgg)
            {
                line++;
                // Ограничим список, чтобы сообщение не превысило лимиты Telegram
                if (line > 50) { msgSb.AppendLine("… (сокращено)"); break; }
                var skuPart = string.IsNullOrWhiteSpace(it.Sku) ? string.Empty : ($"[{it.Sku}] ");
                msgSb.AppendLine($"• {skuPart}{it.Name}: {it.Qty:N3} шт × {it.AvgPrice:N0} = {it.Revenue:N0} UZS");
            }
            msgSb.AppendLine();
            msgSb.AppendLine($"↩️ Возвраты: {returnsCount} на сумму {returnsSum:N0} UZS");
            msgSb.AppendLine($"💳 В долг выдано: {debtsCount} на {debtsSum:N0} UZS");
            msgSb.AppendLine($"📑 Договоры: новых {newContractsCount}, по старым {oldContractsUsedCount}");
            var msg = msgSb.ToString();

            // Подготовка справочников для детальных секций
            // Карта saleItemId -> SaleItem, включая отсутствующие в сегодняшних продажах, если попадут из возвратов
            var saleItemMap = sales.SelectMany(s => s.Items).ToDictionary(si => si.Id, si => si);
            var returnSaleItemIds = returns.SelectMany(r => r.Items).Select(ri => ri.SaleItemId).Distinct().Where(id => !saleItemMap.ContainsKey(id)).ToList();
            if (returnSaleItemIds.Count > 0)
            {
                var extraSis = await db.SaleItems.AsNoTracking().Where(si => returnSaleItemIds.Contains(si.Id)).ToListAsync(ct);
                foreach (var si in extraSis)
                {
                    if (!saleItemMap.ContainsKey(si.Id)) saleItemMap[si.Id] = si;
                }
            }

            // Клиенты для долгов
            var debtClientIds = debts.Select(d => d.ClientId).Distinct().ToList();
            var clients = debtClientIds.Count == 0
                ? new Dictionary<int, ProjectApp.Api.Models.Client>()
                : await db.Clients.AsNoTracking().Where(c => debtClientIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

            string H(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
            List<string> Chunk(string header, IEnumerable<string> rows, int maxLen = 3500)
            {
                var chunks = new List<string>();
                var current = new System.Text.StringBuilder();
                if (!string.IsNullOrWhiteSpace(header)) { current.AppendLine(header); }
                foreach (var row in rows)
                {
                    if (current.Length + row.Length + 1 > maxLen)
                    {
                        chunks.Add(current.ToString());
                        current.Clear();
                        if (!string.IsNullOrWhiteSpace(header)) current.AppendLine(header);
                    }
                    current.AppendLine(row);
                }
                if (current.Length > 0) chunks.Add(current.ToString());
                return chunks;
            }

            // Try to find a top seller photo to attach as a single message with caption
            bool sentAsPhoto = false;
            if (top != null && !string.IsNullOrWhiteSpace(top.Seller))
            {
                try
                {
                    var topPhoto = await db.SalePhotos
                        .AsNoTracking()
                        .Where(p => p.UserName == top.Seller)
                        .OrderByDescending(p => p.CreatedAt)
                        .FirstOrDefaultAsync(ct);
                    if (topPhoto != null && !string.IsNullOrWhiteSpace(topPhoto.PathOrBlob) && System.IO.File.Exists(topPhoto.PathOrBlob))
                    {
                        await using var fs = System.IO.File.OpenRead(topPhoto.PathOrBlob);
                        foreach (var chatId in ids)
                        {
                            fs.Position = 0;
                            try { _ = await tg.SendPhotoAsync(chatId, fs, System.IO.Path.GetFileName(topPhoto.PathOrBlob), msg, "HTML", ct); } catch { }
                        }
                        sentAsPhoto = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "DailySummary: failed to send top seller photo as caption");
                }
            }

            if (!sentAsPhoto)
            {
                foreach (var chatId in ids)
                {
                    try { _ = await tg.SendMessageAsync(chatId, msg, "HTML", null, ct); } catch { }
                }
            }
            
            // Детальные секции
            try
            {
                // Продажи (детально по каждому чеку)
                var saleRows = new List<string>();
                foreach (var s in sales.OrderBy(s => s.CreatedAt))
                {
                    var tLocal = s.CreatedAt + offset;
                    var header = $"<b>Продажа #{s.Id}</b> {tLocal:HH:mm} — <b>{s.Total:N0} UZS</b> — {H(s.PaymentType.ToString())} — клиент: <b>{H(string.IsNullOrWhiteSpace(s.ClientName) ? "-" : s.ClientName)}</b> — менеджер: <b>{H(string.IsNullOrWhiteSpace(s.CreatedBy) ? "-" : s.CreatedBy!)}</b>";
                    saleRows.Add(header);
                    int i = 0; decimal sum = 0m;
                    foreach (var it in s.Items)
                    {
                        i++;
                        var lineSum = it.UnitPrice * it.Qty; sum += lineSum;
                        var skuPart = string.IsNullOrWhiteSpace(it.Sku) ? string.Empty : ($"[{H(it.Sku)}] ");
                        saleRows.Add($"└ {skuPart}{H(it.ProductName ?? $"Product #{it.ProductId}")} — {it.Qty:N3} × {it.UnitPrice:N0} = <b>{lineSum:N0}</b>");
                    }
                }
                foreach (var chunk in Chunk("🧾 <b>Продажи (детали)</b>", saleRows))
                {
                    foreach (var chatId in ids) { try { _ = await tg.SendMessageAsync(chatId, chunk, "HTML", null, ct); } catch { } }
                }

                // Возвраты (детально)
                var retRows = new List<string>();
                foreach (var r in returns.OrderBy(r => r.CreatedAt))
                {
                    var tLocal = r.CreatedAt + offset;
                    retRows.Add($"<b>Возврат #{r.Id}</b> {tLocal:HH:mm} — сумма <b>{r.Sum:N0} UZS</b>");
                    int i = 0;
                    foreach (var ri in r.Items)
                    {
                        i++;
                        saleItemMap.TryGetValue(ri.SaleItemId, out var si);
                        var skuPart = si != null && !string.IsNullOrWhiteSpace(si.Sku) ? $"[{H(si.Sku)}] " : string.Empty;
                        var name = si?.ProductName ?? $"Item #{ri.SaleItemId}";
                        var lineSum = ri.UnitPrice * ri.Qty;
                        retRows.Add($"└ {skuPart}{H(name)} — {ri.Qty:N3} × {ri.UnitPrice:N0} = <b>{lineSum:N0}</b>");
                    }
                }
                if (retRows.Count > 0)
                {
                    foreach (var chunk in Chunk("↩️ <b>Возвраты (детали)</b>", retRows))
                        foreach (var chatId in ids) { try { _ = await tg.SendMessageAsync(chatId, chunk, "HTML", null, ct); } catch { } }
                }

                // Долги (новые)
                var debtRows = new List<string>();
                foreach (var d in debts.OrderBy(d => d.CreatedAt))
                {
                    var tLocal = d.CreatedAt + offset;
                    var clientName = clients.TryGetValue(d.ClientId, out var c) ? H(c.Name) : $"Client #{d.ClientId}";
                    debtRows.Add($"<b>Долг</b> {tLocal:HH:mm} — клиент: <b>{clientName}</b> — сумма <b>{d.OriginalAmount:N0} UZS</b> — по продаже #{d.SaleId}, срок {d.DueDate:dd.MM}");
                }
                if (debtRows.Count > 0)
                {
                    foreach (var chunk in Chunk("💳 <b>Долги (выдано за сутки)</b>", debtRows))
                        foreach (var chatId in ids) { try { _ = await tg.SendMessageAsync(chatId, chunk, "HTML", null, ct); } catch { } }
                }

                // Новые договоры
                var contractRows = new List<string>();
                foreach (var c in newContracts)
                {
                    var tLocal = c.CreatedAt + offset;
                    contractRows.Add($"<b>Договор #{c.Id}</b> {tLocal:HH:mm} — <b>{H(c.OrgName)}</b> — сумма <b>{c.TotalAmount:N0} UZS</b>, позиций: {c.Items.Count}");
                    int i = 0;
                    foreach (var it in c.Items)
                    {
                        i++;
                        var skuPart = string.IsNullOrWhiteSpace(it.Sku) ? string.Empty : ($"[{H(it.Sku)}] ");
                        contractRows.Add($"└ {skuPart}{H(it.Name)} — {it.Qty:N3} × {it.UnitPrice:N0} = <b>{(it.UnitPrice * it.Qty):N0}</b>");
                    }
                }
                if (contractRows.Count > 0)
                {
                    foreach (var chunk in Chunk("📑 <b>Новые договоры</b>", contractRows))
                        foreach (var chatId in ids) { try { _ = await tg.SendMessageAsync(chatId, chunk, "HTML", null, ct); } catch { } }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DailySummary: failed to send detailed sections");
            }

            _logger.LogInformation("DailySummary: sent summary for {Date}", periodStr);

            // After daily summary: delete all stored sale photos
            try
            {
                var photos = await db.SalePhotos.AsNoTracking().ToListAsync(ct);
                foreach (var p in photos)
                {
                    try { if (!string.IsNullOrWhiteSpace(p.PathOrBlob) && System.IO.File.Exists(p.PathOrBlob)) System.IO.File.Delete(p.PathOrBlob); } catch { }
                }
                db.SalePhotos.RemoveRange(db.SalePhotos);
                await db.SaveChangesAsync(ct);
            }
            catch { }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DailySummary: failed to send summary");
        }
    }
}
