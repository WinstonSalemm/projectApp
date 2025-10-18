using ProjectApp.Api.Data;
using ProjectApp.Api.Integrations.Telegram;
using ProjectApp.Api.Integrations.Email;
using ProjectApp.Api.Models;
using Microsoft.EntityFrameworkCore;

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

    public AutoReportsService(
        AppDbContext db,
        ITelegramService telegram,
        IEmailService email,
        OwnerDashboardService dashboardService,
        ILogger<AutoReportsService> logger)
    {
        _db = db;
        _telegram = telegram;
        _email = email;
        _dashboardService = dashboardService;
        _logger = logger;
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
                        .Select(pr => new { pr.Sku, pr.Price })
                        .FirstOrDefaultAsync();
                    
                    // Считаем остаток
                    var stock = await _db.Batches
                        .Where(b => b.Product.Name == p.ProductName && b.Qty > 0)
                        .SumAsync(b => (int?)b.Qty) ?? 0;
                    
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
                        .Select(pr => new { pr.Sku, pr.Price })
                        .FirstOrDefaultAsync();
                    
                    // Считаем остаток
                    var stock = await _db.Batches
                        .Where(b => b.Product.Name == p.ProductName && b.Qty > 0)
                        .SumAsync(b => (int?)b.Qty) ?? 0;
                    
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
}
