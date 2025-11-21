using ProjectApp.Api.Data;
using ProjectApp.Api.Integrations.Telegram;
using ProjectApp.Api.Integrations.Email;
using ProjectApp.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ClosedXML.Excel;
using System.IO;

namespace ProjectApp.Api.Services;

/// <summary>
/// Сервис для автоматической генерации и отправки отчетов
/// </summary>
public class AutoReportsService
{
    private readonly AppDbContext _db;
    private readonly ITelegramService _telegram;
    private readonly IEmailService _email;
    private readonly OwnerDashboardService _dashboardService;
    private readonly ILogger<AutoReportsService> _logger;
    private readonly TelegramSettings _tgSettings;

    public AutoReportsService(
        AppDbContext db,
        ITelegramService telegram,
        IEmailService email,
        OwnerDashboardService dashboardService,
        IOptions<TelegramSettings> tgOptions,
        ILogger<AutoReportsService> logger)
    {
        _db = db;
        _telegram = telegram;
        _email = email;
        _dashboardService = dashboardService;
        _logger = logger;
        _tgSettings = tgOptions.Value;
    }

    /// <summary>
    /// Отправить развернутый отчёт за произвольный период (продукты и менеджеры)
    /// </summary>
    public async Task SendDetailedPeriodReportAsync(DateTime fromUtc, DateTime toUtc, long chatId)
    {
        try
        {
            // Итоги периода
            var saleRows = await _db.Sales
                .AsNoTracking()
                .Where(s => s.CreatedAt >= fromUtc && s.CreatedAt < toUtc)
                .Select(s => new { s.Id, s.Total, Qty = s.Items.Sum(i => i.Qty) })
                .ToListAsync();

            if (saleRows.Count == 0)
            {
                await _telegram.SendMessageAsync(chatId, "📊 Развернутый отчёт: за период продаж нет");
                return;
            }

            var totalAmount = saleRows.Sum(r => r.Total);
            var totalQty = saleRows.Sum(r => r.Qty);
            var count = saleRows.Count;
            var header = $"📊 Развернутый отчёт\nПериод: {fromUtc:yyyy-MM-dd}..{toUtc:yyyy-MM-dd}\nОборот: {totalAmount:N0} UZS\nШтук: {totalQty:N0}\nЧеки: {count}";
            await _telegram.SendMessageAsync(chatId, header);

            // Разбивка по товарам
            var productAgg = await (from s in _db.Sales
                                    where s.CreatedAt >= fromUtc && s.CreatedAt < toUtc
                                    join si in _db.SaleItems on s.Id equals si.SaleId
                                    join p in _db.Products on si.ProductId equals p.Id
                                    group new { si, p } by new { si.ProductId, p.Name } into g
                                    select new
                                    {
                                        Name = g.Key.Name,
                                        Qty = g.Sum(x => x.si.Qty),
                                        Revenue = g.Sum(x => x.si.Qty * x.si.UnitPrice)
                                    })
                                    .OrderByDescending(x => x.Revenue)
                                    .ToListAsync();

            if (productAgg.Count > 0)
            {
                const int maxChars = 3500;
                var sb = new System.Text.StringBuilder("🧾 Товары за период:\n\n");
                foreach (var p in productAgg)
                {
                    var avg = p.Qty > 0 ? p.Revenue / p.Qty : 0;
                    var line = $"• {p.Name} — {p.Qty:N0} шт × {avg:N0} = {p.Revenue:N0}";
                    if (sb.Length + line.Length + 1 > maxChars)
                    {
                        await _telegram.SendMessageAsync(chatId, sb.ToString());
                        sb.Clear();
                        sb.AppendLine("🧾 Товары (продолжение):\n");
                    }
                    sb.AppendLine(line);
                }
                if (sb.Length > 0)
                    await _telegram.SendMessageAsync(chatId, sb.ToString());
            }

            // Разбивка по менеджерам
            var managers = await (from s in _db.Sales
                                  where s.CreatedAt >= fromUtc && s.CreatedAt < toUtc
                                  group s by s.CreatedBy into g
                                  select new
                                  {
                                      Manager = g.Key ?? "unknown",
                                      SalesCount = g.Count(),
                                      Revenue = g.Sum(x => x.Total)
                                  })
                                  .OrderByDescending(x => x.Revenue)
                                  .ToListAsync();

            if (managers.Count > 0)
            {
                var sb = new System.Text.StringBuilder("👨‍💼 Менеджеры за период:\n\n");
                foreach (var m in managers)
                {
                    sb.AppendLine($"• {m.Manager}: {m.Revenue:N0} UZS ({m.SalesCount} чек.)");
                }
                await _telegram.SendMessageAsync(chatId, sb.ToString());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки развернутого отчёта");
        }
    }

    /// <summary>
    /// Отправить ежедневный отчет владельцу
    /// </summary>
    public async Task SendDailyReportAsync()
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            // Получаем данные за день
            var dashboard = await _dashboardService.GetDashboardAsync(today);

            // Формируем сообщение
            var message = $"📊 <b>ЕЖЕДНЕВНЫЙ ОТЧЕТ</b>\n";
            message += $"📅 {today:dd.MM.yyyy}\n\n";

            message += "💰 <b>ФИНАНСЫ:</b>\n";
            message += $"├ Выручка: <b>{dashboard.TodayRevenue:N0} UZS</b>\n";
            message += $"├ Прибыль: <b>{dashboard.TodayProfit:N0} UZS</b>\n";
            message += $"├ Продаж: <b>{dashboard.TodaySalesCount}</b>\n";
            message += $"└ Средний чек: <b>{dashboard.TodayAverageCheck:N0} UZS</b>\n\n";

            message += "💵 <b>КАССЫ:</b>\n";
            if (dashboard.CashboxBalances.Any())
            {
                foreach (var cb in dashboard.CashboxBalances.OrderByDescending(x => x.Value))
                {
                    message += $"├ {cb.Key}: <b>{cb.Value:N0}</b>\n";
                }
            }
            else
            {
                message += "├ Нет данных\n";
            }
            message += $"└ Всего: <b>{dashboard.TotalCash:N0} UZS</b>\n\n";

            message += "📦 <b>СКЛАД:</b>\n";
            message += $"├ Стоимость товара: <b>{dashboard.InventoryValue:N0} UZS</b>\n";
            message += $"└ Критических остатков: <b>{dashboard.CriticalStockAlerts.Count}</b>\n\n";

            message += "💸 <b>ДОЛГИ:</b>\n";
            message += $"├ Клиенты должны: <b>{dashboard.ClientDebts:N0} UZS</b>\n";
            message += $"└ Просроченных: <b>{dashboard.OverdueDebts.Count}</b>\n\n";

            if (dashboard.Top5ProductsToday.Any())
            {
                message += "🏆 <b>ТОП-5 ТОВАРОВ ДНЯ:</b>\n";
                for (int i = 0; i < Math.Min(5, dashboard.Top5ProductsToday.Count); i++)
                {
                    var p = dashboard.Top5ProductsToday[i];
                    
                    // Получаем детали товара из БД
                    var product = await _db.Products
                        .Where(pr => pr.Name == p.ProductName)
                        .Select(pr => new { pr.Id, pr.Sku, pr.Price })
                        .FirstOrDefaultAsync();
                    
                    // Считаем остаток
                    var stock = product != null
                        ? await _db.Batches
                            .Where(b => b.ProductId == product.Id && b.Qty > 0)
                            .SumAsync(b => (int?)b.Qty) ?? 0
                        : 0;
                    
                    var sku = product?.Sku ?? "N/A";
                    var avgPrice = p.TotalQuantity > 0 ? p.TotalRevenue / p.TotalQuantity : 0;
                    
                    message += $"{i + 1}. <b>{p.ProductName}</b>\n";
                    message += $"   📦 SKU: {sku}\n";
                    message += $"   💰 Выручка: {p.TotalRevenue:N0} UZS\n";
                    message += $"   🔢 Продано: {p.TotalQuantity} шт × {avgPrice:N0} UZS\n";
                    message += $"   📊 Остаток: {stock} шт\n";
                }
            }
            else
            {
                message += "🏆 <b>ТОП-5 ТОВАРОВ:</b> нет продаж\n";
            }

            message += $"\n⏰ Отчет сгенерирован: {DateTime.UtcNow:HH:mm}";

            // Отправляем в Telegram
            await _telegram.SendMessageToOwnerAsync(message);
            
            // Отправляем на Email (HTML-версия)
            var emailHtml = EmailTemplates.DailyReport(dashboard);
            await _email.SendToOwnerAsync($"📊 Ежедневный отчет за {today:dd.MM.yyyy}", emailHtml);
            
            _logger.LogInformation($"Ежедневный отчет за {today:dd.MM.yyyy} отправлен (Telegram + Email)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки ежедневного отчета");
        }
    }

    /// <summary>
    /// Отправить еженедельный отчет владельцу
    /// </summary>
    public async Task SendWeeklyReportAsync()
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var weekStart = today.AddDays(-(int)today.DayOfWeek + 1); // Понедельник
            var weekEnd = today;

            // Статистика за неделю
            var sales = await _db.Sales
                .Where(s => s.CreatedAt >= weekStart && s.CreatedAt < weekEnd.AddDays(1))
                .ToListAsync();

            var totalRevenue = sales.Sum(s => s.Total);
            var salesCount = sales.Count;
            var avgCheck = salesCount > 0 ? totalRevenue / salesCount : 0;

            // Топ товары за неделю
            var topProducts = await (from sale in _db.Sales
                                    where sale.CreatedAt >= weekStart && sale.CreatedAt < weekEnd.AddDays(1)
                                    join saleItem in _db.SaleItems on sale.Id equals saleItem.SaleId
                                    join product in _db.Products on saleItem.ProductId equals product.Id
                                    group saleItem by new { saleItem.ProductId, product.Name } into g
                                    select new
                                    {
                                        ProductName = g.Key.Name,
                                        TotalRevenue = g.Sum(si => si.UnitPrice * si.Qty),
                                        TotalQty = g.Sum(si => si.Qty)
                                    })
                .OrderByDescending(p => p.TotalRevenue)
                .Take(10)
                .ToListAsync();

            // Топ менеджеры
            var topManagers = await (from s in _db.Sales
                                    where s.CreatedAt >= weekStart && s.CreatedAt < weekEnd.AddDays(1) && s.CreatedBy != null
                                    group s by s.CreatedBy into g
                                    select new
                                    {
                                        Manager = g.Key,
                                        SalesCount = g.Count(),
                                        TotalRevenue = g.Sum(s => s.Total)
                                    })
                .OrderByDescending(m => m.TotalRevenue)
                .Take(5)
                .ToListAsync();

            // Формируем отчет
            var message = $"📊 <b>ЕЖЕНЕДЕЛЬНЫЙ ОТЧЕТ</b>\n";
            message += $"📅 {weekStart:dd.MM} - {weekEnd:dd.MM.yyyy}\n\n";

            message += "💰 <b>СВОДКА ЗА НЕДЕЛЮ:</b>\n";
            message += $"├ Выручка: <b>{totalRevenue:N0} UZS</b>\n";
            message += $"├ Продаж: <b>{salesCount}</b>\n";
            message += $"└ Средний чек: <b>{avgCheck:N0} UZS</b>\n\n";

            if (topProducts.Any())
            {
                message += "🏆 <b>ТОП-10 ТОВАРОВ НЕДЕЛИ:</b>\n";
                for (int i = 0; i < topProducts.Count; i++)
                {
                    var p = topProducts[i];
                    
                    // Получаем детали товара
                    var product = await _db.Products
                        .Where(pr => pr.Name == p.ProductName)
                        .Select(pr => new { pr.Id, pr.Sku, pr.Price })
                        .FirstOrDefaultAsync();
                    
                    // Считаем остаток
                    var stock = product != null
                        ? await _db.Batches
                            .Where(b => b.ProductId == product.Id && b.Qty > 0)
                            .SumAsync(b => (int?)b.Qty) ?? 0
                        : 0;
                    
                    var sku = product?.Sku ?? "N/A";
                    var avgPrice = p.TotalQty > 0 ? p.TotalRevenue / p.TotalQty : 0;
                    
                    message += $"{i + 1}. <b>{p.ProductName}</b>\n";
                    message += $"   📦 SKU: {sku}\n";
                    message += $"   💰 Выручка: {p.TotalRevenue:N0} UZS\n";
                    message += $"   🔢 Продано: {p.TotalQty} шт × {avgPrice:N0} UZS\n";
                    message += $"   📊 Остаток: {stock} шт\n";
                }
                message += "\n";
            }

            if (topManagers.Any())
            {
                message += "👨‍💼 <b>ТОП МЕНЕДЖЕРЫ:</b>\n";
                for (int i = 0; i < topManagers.Count; i++)
                {
                    var m = topManagers[i];
                    message += $"{i + 1}. {m.Manager}\n";
                    message += $"   💰 {m.TotalRevenue:N0} UZS ({m.SalesCount} продаж)\n";
                }
                message += "\n";
            }

            // Долги
            var totalDebts = await _db.Debts
                .Where(d => d.Status == DebtStatus.Open)
                .SumAsync(d => (decimal?)d.Amount) ?? 0m;

            message += "💸 <b>ДОЛГИ:</b>\n";
            message += $"└ Клиенты должны: <b>{totalDebts:N0} UZS</b>\n\n";

            message += $"⏰ Отчет сгенерирован: {DateTime.UtcNow:dd.MM.yyyy HH:mm}";

            await _telegram.SendMessageToOwnerAsync(message);
            _logger.LogInformation($"Еженедельный отчет за {weekStart:dd.MM}-{weekEnd:dd.MM} отправлен");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки еженедельного отчета");
        }
    }

    /// <summary>
    /// Отправить в Telegram список остатков на конец дня (по всем позициям)
    /// </summary>
    public async Task SendEndOfDayStockAsync(long? chatId = null)
    {
        try
        {
            var stocks = await (from s in _db.Stocks.AsNoTracking()
                                where s.Register == StockRegister.IM40 || s.Register == StockRegister.ND40
                                group s by s.ProductId into g
                                select new { ProductId = g.Key, Qty = g.Sum(x => x.Qty) })
                               .ToListAsync();

            if (stocks.Count == 0)
            {
                if (chatId.HasValue) await _telegram.SendMessageAsync(chatId.Value, "📦 Остатки на конец дня: нет данных");
                else await _telegram.SendMessageToOwnerAsync("📦 Остатки на конец дня: нет данных");
                return;
            }

            var productIds = stocks.Select(x => x.ProductId).ToList();
            var products = await _db.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name })
                .ToDictionaryAsync(p => p.Id, p => p.Name);

            var lines = stocks
                .Where(x => x.Qty > 0)
                .Select(x => new
                {
                    Name = products.TryGetValue(x.ProductId, out var name) ? name : $"#{x.ProductId}",
                    Qty = x.Qty
                })
                .OrderBy(x => x.Name)
                .Select(x => $"{x.Name} - {x.Qty:N0} шт")
                .ToList();

            if (lines.Count == 0)
            {
                if (chatId.HasValue) await _telegram.SendMessageAsync(chatId.Value, "📦 Остатки на конец дня: все нулевые");
                else await _telegram.SendMessageToOwnerAsync("📦 Остатки на конец дня: все нулевые");
                return;
            }

            const int maxChars = 3500;
            var header = $"📦 Остатки на конец дня ({DateTime.UtcNow:yyyy-MM-dd} UTC)\n\n";
            var current = new System.Text.StringBuilder(header);
            var pages = new List<string>();
            foreach (var line in lines)
            {
                if (current.Length + line.Length + 1 > maxChars)
                {
                    pages.Add(current.ToString());
                    current.Clear();
                    current.Append("📦 Остатки (продолжение)\n\n");
                }
                current.AppendLine(line);
            }
            if (current.Length > 0) pages.Add(current.ToString());

            foreach (var msg in pages)
            {
                if (chatId.HasValue) await _telegram.SendMessageAsync(chatId.Value, msg);
                else await _telegram.SendMessageToOwnerAsync(msg);
            }

            _logger.LogInformation($"Отправлен список остатков на конец дня: {lines.Count} позиций, сообщений: {pages.Count}");

            // Определяем товары, которые сегодня закончились (были >0 на начало дня и стали 0 сейчас)
            var offset = TimeSpan.FromMinutes(_tgSettings.TimeZoneOffsetMinutes);
            var nowUtc = DateTime.UtcNow;
            var localToday = (nowUtc + offset).Date;
            var startUtc = localToday - offset;
            var endUtc = localToday.AddDays(1) - offset;

            var lastSnapshotTs = await _db.StockSnapshots
                .Where(x => x.CreatedAt < startUtc)
                .MaxAsync(x => (DateTime?)x.CreatedAt) ?? null;

            if (lastSnapshotTs != null)
            {
                var startRows = await _db.StockSnapshots
                    .AsNoTracking()
                    .Where(x => x.CreatedAt == lastSnapshotTs)
                    .Select(x => new { x.ProductId, x.TotalQty })
                    .ToListAsync();

                var startedPositive = startRows.Where(r => r.TotalQty > 0).Select(r => r.ProductId).ToList();
                if (startedPositive.Count > 0)
                {
                    var currentTotals = await _db.Stocks.AsNoTracking()
                        .Where(s => startedPositive.Contains(s.ProductId) && (s.Register == StockRegister.IM40 || s.Register == StockRegister.ND40))
                        .GroupBy(s => s.ProductId)
                        .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Qty) })
                        .ToDictionaryAsync(x => x.ProductId, x => x.Qty);

                    var depleted = startedPositive
                        .Where(pid => !currentTotals.TryGetValue(pid, out var q) || q <= 0)
                        .ToList();

                    if (depleted.Count > 0)
                    {
                        var prodMap = await _db.Products.AsNoTracking()
                            .Where(p => depleted.Contains(p.Id))
                            .Select(p => new { p.Id, p.Name })
                            .ToDictionaryAsync(p => p.Id, p => p.Name);

                        var lines2 = new List<string>();
                        foreach (var pid in depleted)
                        {
                            var last = await (from si in _db.SaleItems
                                              join s in _db.Sales on si.SaleId equals s.Id
                                              where si.ProductId == pid && s.CreatedAt >= startUtc && s.CreatedAt < endUtc
                                              orderby s.CreatedAt descending
                                              select new { s.Id, s.CreatedAt, s.CreatedBy, s.ClientName, si.Qty, si.UnitPrice }).FirstOrDefaultAsync();

                            if (last != null)
                            {
                                var name = prodMap.TryGetValue(pid, out var n) ? n : $"#{pid}";
                                var sum = last.Qty * last.UnitPrice;
                                var tsLocal = last.CreatedAt + offset;
                                lines2.Add($"{name} — последняя продажа #{last.Id} от {tsLocal:HH:mm}, {last.Qty:N0} шт × {last.UnitPrice:N0} = {sum:N0}");
                            }
                        }

                        if (lines2.Count > 0)
                        {
                            var header2 = "⚠️ Сегодня закончились (последняя продажа):\n\n";
                            var sb = new System.Text.StringBuilder(header2);
                            foreach (var line in lines2)
                                sb.AppendLine(line);
                            var text = sb.ToString();
                            if (chatId.HasValue) await _telegram.SendMessageAsync(chatId.Value, text);
                            else await _telegram.SendMessageToOwnerAsync(text);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки списка остатков на конец дня");
        }
    }

    /// <summary>
    /// Сформировать и отправить Excel-отчёт за период: продажи (позиции), товары, менеджеры, возвраты, долги
    /// </summary>
    public async Task SendExcelPeriodReportAsync(DateTime fromUtc, DateTime toUtc, long chatId)
    {
        try
        {
            var offset = TimeSpan.FromMinutes(_tgSettings.TimeZoneOffsetMinutes);
            var periodLabel = $"{(fromUtc + offset):yyyy-MM-dd}..{(toUtc + offset).AddDays(-1):yyyy-MM-dd}";

            // Sales brief
            var salesBrief = await _db.Sales
                .AsNoTracking()
                .Where(s => s.CreatedAt >= fromUtc && s.CreatedAt < toUtc)
                .Select(s => new { s.Id, s.CreatedAt, s.CreatedBy, s.ClientName, s.PaymentType, s.Total })
                .ToListAsync();

            // Sales items with product details
            var saleItems = await (from s in _db.Sales
                                   where s.CreatedAt >= fromUtc && s.CreatedAt < toUtc
                                   join si in _db.SaleItems on s.Id equals si.SaleId
                                   join p in _db.Products on si.ProductId equals p.Id into gp
                                   from p in gp.DefaultIfEmpty()
                                   select new
                                   {
                                       s.Id,
                                       s.CreatedAt,
                                       s.CreatedBy,
                                       s.ClientName,
                                       s.PaymentType,
                                       si.ProductId,
                                       Sku = si.Sku ?? p!.Sku,
                                       Name = si.ProductName ?? p!.Name,
                                       si.Qty,
                                       si.UnitPrice,
                                       si.Cost
                                   }).ToListAsync();

            // Returns
            var returnItems = await (from r in _db.Returns
                                     where r.CreatedAt >= fromUtc && r.CreatedAt < toUtc
                                     join ri in _db.ReturnItems on r.Id equals ri.ReturnId
                                     join si in _db.SaleItems on ri.SaleItemId equals si.Id
                                     join p in _db.Products on si.ProductId equals p.Id into gp
                                     from p in gp.DefaultIfEmpty()
                                     join c in _db.Clients on r.ClientId equals c.Id into gc
                                     from c in gc.DefaultIfEmpty()
                                     select new
                                     {
                                         r.Id,
                                         r.CreatedAt,
                                         r.RefSaleId,
                                         ClientName = c != null ? c.Name : "",
                                         Name = si.ProductName ?? p!.Name,
                                         Sku = si.Sku ?? p!.Sku,
                                         ri.Qty,
                                         ri.UnitPrice
                                     }).ToListAsync();

            // Debts and payments
            var debts = await _db.Debts.AsNoTracking()
                .Where(d => d.CreatedAt >= fromUtc && d.CreatedAt < toUtc)
                .Join(_db.Clients, d => d.ClientId, cl => cl.Id, (d, cl) => new
                {
                    d.Id, d.SaleId, d.OriginalAmount, d.Amount, d.DueDate, d.Status, d.CreatedAt, d.CreatedBy,
                    ClientName = cl.Name
                }).ToListAsync();

            var debtPayments = await _db.DebtPayments.AsNoTracking()
                .Where(p => p.PaidAt >= fromUtc && p.PaidAt < toUtc)
                .ToListAsync();

            // Aggregations
            var revenue = salesBrief.Sum(s => s.Total);
            var itemRevenue = saleItems.Sum(x => x.Qty * x.UnitPrice);
            var itemCost = saleItems.Sum(x => x.Qty * x.Cost);
            var profit = itemRevenue - itemCost;

            var retAmount = returnItems.Sum(r => r.Qty * r.UnitPrice);
            var debtsCreated = debts.Sum(d => d.OriginalAmount);
            var debtsPaid = debtPayments.Sum(p => p.Amount);
            var outstanding = await _db.Debts.AsNoTracking()
                .Where(d => d.Status == DebtStatus.Open)
                .SumAsync(d => (decimal?)d.Amount) ?? 0m;

            using var wb = new XLWorkbook();

            // Summary
            var ws0 = wb.AddWorksheet("Summary");
            int r0 = 1;
            ws0.Cell(r0++, 1).Value = "Период"; ws0.Cell(r0 - 1, 2).Value = periodLabel;
            ws0.Cell(r0++, 1).Value = "Выручка (чеки)"; ws0.Cell(r0 - 1, 2).Value = revenue; ws0.Cell(r0 - 1, 2).Style.NumberFormat.Format = "#,##0";
            ws0.Cell(r0++, 1).Value = "Выручка (позиции)"; ws0.Cell(r0 - 1, 2).Value = itemRevenue; ws0.Cell(r0 - 1, 2).Style.NumberFormat.Format = "#,##0";
            ws0.Cell(r0++, 1).Value = "Себестоимость"; ws0.Cell(r0 - 1, 2).Value = itemCost; ws0.Cell(r0 - 1, 2).Style.NumberFormat.Format = "#,##0";
            ws0.Cell(r0++, 1).Value = "Валовая прибыль"; ws0.Cell(r0 - 1, 2).Value = profit; ws0.Cell(r0 - 1, 2).Style.NumberFormat.Format = "#,##0";
            ws0.Cell(r0++, 1).Value = "Возвраты"; ws0.Cell(r0 - 1, 2).Value = retAmount; ws0.Cell(r0 - 1, 2).Style.NumberFormat.Format = "#,##0";
            ws0.Cell(r0++, 1).Value = "Долги (создано)"; ws0.Cell(r0 - 1, 2).Value = debtsCreated; ws0.Cell(r0 - 1, 2).Style.NumberFormat.Format = "#,##0";
            ws0.Cell(r0++, 1).Value = "Оплаты долгов"; ws0.Cell(r0 - 1, 2).Value = debtsPaid; ws0.Cell(r0 - 1, 2).Style.NumberFormat.Format = "#,##0";
            ws0.Cell(r0++, 1).Value = "Открытые долги (текущ.)"; ws0.Cell(r0 - 1, 2).Value = outstanding; ws0.Cell(r0 - 1, 2).Style.NumberFormat.Format = "#,##0";
            ws0.Columns().AdjustToContents();

            // Sales items
            var ws1 = wb.AddWorksheet("SalesItems");
            int r1 = 1;
            ws1.Cell(r1, 1).Value = "Дата";
            ws1.Cell(r1, 2).Value = "SaleId";
            ws1.Cell(r1, 3).Value = "SKU";
            ws1.Cell(r1, 4).Value = "Товар";
            ws1.Cell(r1, 5).Value = "Кол-во";
            ws1.Cell(r1, 6).Value = "Цена";
            ws1.Cell(r1, 7).Value = "Сумма";
            ws1.Cell(r1, 8).Value = "Себест. ед.";
            ws1.Cell(r1, 9).Value = "Себест. сумма";
            ws1.Cell(r1, 10).Value = "Прибыль";
            ws1.Cell(r1, 11).Value = "Менеджер";
            ws1.Cell(r1, 12).Value = "Клиент";
            ws1.Cell(r1, 13).Value = "Оплата";
            r1++;
            foreach (var x in saleItems.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id))
            {
                var dateLocal = x.CreatedAt + offset;
                var sum = x.Qty * x.UnitPrice; var costSum = x.Qty * x.Cost; var pr = sum - costSum;
                ws1.Cell(r1, 1).Value = dateLocal; ws1.Cell(r1, 1).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
                ws1.Cell(r1, 2).Value = x.Id;
                ws1.Cell(r1, 3).Value = x.Sku;
                ws1.Cell(r1, 4).Value = x.Name;
                ws1.Cell(r1, 5).Value = x.Qty; ws1.Cell(r1, 5).Style.NumberFormat.Format = "#,##0";
                ws1.Cell(r1, 6).Value = x.UnitPrice; ws1.Cell(r1, 6).Style.NumberFormat.Format = "#,##0";
                ws1.Cell(r1, 7).Value = sum; ws1.Cell(r1, 7).Style.NumberFormat.Format = "#,##0";
                ws1.Cell(r1, 8).Value = x.Cost; ws1.Cell(r1, 8).Style.NumberFormat.Format = "#,##0";
                ws1.Cell(r1, 9).Value = costSum; ws1.Cell(r1, 9).Style.NumberFormat.Format = "#,##0";
                ws1.Cell(r1, 10).Value = pr; ws1.Cell(r1, 10).Style.NumberFormat.Format = "#,##0";
                ws1.Cell(r1, 11).Value = x.CreatedBy;
                ws1.Cell(r1, 12).Value = x.ClientName;
                ws1.Cell(r1, 13).Value = x.PaymentType.ToString();
                r1++;
            }
            ws1.Columns().AdjustToContents();

            // Products
            var ws2 = wb.AddWorksheet("Products");
            int r2 = 1;
            ws2.Cell(r2, 1).Value = "Товар";
            ws2.Cell(r2, 2).Value = "Кол-во";
            ws2.Cell(r2, 3).Value = "Выручка";
            ws2.Cell(r2, 4).Value = "Средняя цена";
            ws2.Cell(r2, 5).Value = "Себест. сумма";
            ws2.Cell(r2, 6).Value = "Прибыль";
            r2++;
            var prodAgg = saleItems
                .GroupBy(x => x.Name)
                .Select(g => new { Name = g.Key, Qty = g.Sum(z => z.Qty), Revenue = g.Sum(z => z.Qty * z.UnitPrice), Cost = g.Sum(z => z.Qty * z.Cost) })
                .OrderByDescending(a => a.Revenue)
                .ToList();
            foreach (var p in prodAgg)
            {
                var avg = p.Qty > 0 ? p.Revenue / p.Qty : 0m;
                ws2.Cell(r2, 1).Value = p.Name;
                ws2.Cell(r2, 2).Value = p.Qty; ws2.Cell(r2, 2).Style.NumberFormat.Format = "#,##0";
                ws2.Cell(r2, 3).Value = p.Revenue; ws2.Cell(r2, 3).Style.NumberFormat.Format = "#,##0";
                ws2.Cell(r2, 4).Value = avg; ws2.Cell(r2, 4).Style.NumberFormat.Format = "#,##0";
                ws2.Cell(r2, 5).Value = p.Cost; ws2.Cell(r2, 5).Style.NumberFormat.Format = "#,##0";
                ws2.Cell(r2, 6).Value = p.Revenue - p.Cost; ws2.Cell(r2, 6).Style.NumberFormat.Format = "#,##0";
                r2++;
            }
            ws2.Columns().AdjustToContents();

            // Managers
            var ws3 = wb.AddWorksheet("Managers");
            int r3 = 1;
            ws3.Cell(r3, 1).Value = "Менеджер";
            ws3.Cell(r3, 2).Value = "Чеков";
            ws3.Cell(r3, 3).Value = "Оборот";
            r3++;
            var manAgg = salesBrief
                .GroupBy(s => s.CreatedBy ?? "unknown")
                .Select(g => new { Manager = g.Key, Count = g.Count(), Revenue = g.Sum(z => z.Total) })
                .OrderByDescending(x => x.Revenue).ToList();
            foreach (var m in manAgg)
            { ws3.Cell(r3, 1).Value = m.Manager; ws3.Cell(r3, 2).Value = m.Count; ws3.Cell(r3, 3).Value = m.Revenue; ws3.Cell(r3, 3).Style.NumberFormat.Format = "#,##0"; r3++; }
            ws3.Columns().AdjustToContents();

            // Returns
            var ws4 = wb.AddWorksheet("Returns");
            int r4 = 1;
            ws4.Cell(r4, 1).Value = "Дата";
            ws4.Cell(r4, 2).Value = "ReturnId";
            ws4.Cell(r4, 3).Value = "SKU";
            ws4.Cell(r4, 4).Value = "Товар";
            ws4.Cell(r4, 5).Value = "Кол-во";
            ws4.Cell(r4, 6).Value = "Цена";
            ws4.Cell(r4, 7).Value = "Сумма";
            ws4.Cell(r4, 8).Value = "RefSaleId";
            ws4.Cell(r4, 9).Value = "Клиент";
            r4++;
            foreach (var r in returnItems.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id))
            {
                var local = r.CreatedAt + offset; var sum = r.Qty * r.UnitPrice;
                ws4.Cell(r4, 1).Value = local; ws4.Cell(r4, 1).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
                ws4.Cell(r4, 2).Value = r.Id;
                ws4.Cell(r4, 3).Value = r.Sku;
                ws4.Cell(r4, 4).Value = r.Name;
                ws4.Cell(r4, 5).Value = r.Qty; ws4.Cell(r4, 5).Style.NumberFormat.Format = "#,##0";
                ws4.Cell(r4, 6).Value = r.UnitPrice; ws4.Cell(r4, 6).Style.NumberFormat.Format = "#,##0";
                ws4.Cell(r4, 7).Value = sum; ws4.Cell(r4, 7).Style.NumberFormat.Format = "#,##0";
                ws4.Cell(r4, 8).Value = r.RefSaleId;
                ws4.Cell(r4, 9).Value = r.ClientName;
                r4++;
            }
            ws4.Columns().AdjustToContents();

            // Debts
            var ws5 = wb.AddWorksheet("Debts"); int r5 = 1;
            ws5.Cell(r5, 1).Value = "Дата";
            ws5.Cell(r5, 2).Value = "DebtId";
            ws5.Cell(r5, 3).Value = "Client";
            ws5.Cell(r5, 4).Value = "SaleId";
            ws5.Cell(r5, 5).Value = "Original";
            ws5.Cell(r5, 6).Value = "Remaining";
            ws5.Cell(r5, 7).Value = "DueDate";
            ws5.Cell(r5, 8).Value = "Status";
            ws5.Cell(r5, 9).Value = "CreatedBy";
            r5++;
            foreach (var d in debts.OrderBy(x => x.CreatedAt))
            {
                ws5.Cell(r5, 1).Value = d.CreatedAt + offset; ws5.Cell(r5, 1).Style.DateFormat.Format = "yyyy-MM-dd";
                ws5.Cell(r5, 2).Value = d.Id;
                ws5.Cell(r5, 3).Value = d.ClientName;
                ws5.Cell(r5, 4).Value = d.SaleId;
                ws5.Cell(r5, 5).Value = d.OriginalAmount; ws5.Cell(r5, 5).Style.NumberFormat.Format = "#,##0";
                ws5.Cell(r5, 6).Value = d.Amount; ws5.Cell(r5, 6).Style.NumberFormat.Format = "#,##0";
                ws5.Cell(r5, 7).Value = d.DueDate + offset; ws5.Cell(r5, 7).Style.DateFormat.Format = "yyyy-MM-dd";
                ws5.Cell(r5, 8).Value = d.Status.ToString();
                ws5.Cell(r5, 9).Value = d.CreatedBy;
                r5++;
            }
            ws5.Columns().AdjustToContents();

            // Debt payments
            var ws6 = wb.AddWorksheet("DebtPayments"); int r6 = 1;
            ws6.Cell(r6, 1).Value = "PaidAt";
            ws6.Cell(r6, 2).Value = "DebtId";
            ws6.Cell(r6, 3).Value = "Amount";
            ws6.Cell(r6, 4).Value = "Method";
            ws6.Cell(r6, 5).Value = "CreatedBy";
            ws6.Cell(r6, 6).Value = "Comment";
            r6++;
            foreach (var p in debtPayments.OrderBy(x => x.PaidAt))
            {
                ws6.Cell(r6, 1).Value = p.PaidAt + offset; ws6.Cell(r6, 1).Style.DateFormat.Format = "yyyy-MM-dd";
                ws6.Cell(r6, 2).Value = p.DebtId;
                ws6.Cell(r6, 3).Value = p.Amount; ws6.Cell(r6, 3).Style.NumberFormat.Format = "#,##0";
                ws6.Cell(r6, 4).Value = p.Method;
                ws6.Cell(r6, 5).Value = p.CreatedBy;
                ws6.Cell(r6, 6).Value = p.Comment;
                r6++;
            }
            ws6.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms); ms.Position = 0;
            var fileName = $"report-{periodLabel.Replace(':','-').Replace(' ','_')}.xlsx";
            await _telegram.SendDocumentAsync(chatId, ms, fileName, caption: $"📊 Excel-отчёт {periodLabel}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка формирования Excel отчёта");
        }
    }
}
