using ProjectApp.Api.Data;
using ProjectApp.Api.Integrations.Telegram;
using ProjectApp.Api.Integrations.Email;
using ProjectApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ProjectApp.Api.Services;

/// <summary>
/// Сервис для мониторинга и отправки алертов владельцу
/// </summary>
public class AlertsService
{
    private readonly AppDbContext _db;
    private readonly ITelegramService _telegram;
    private readonly IEmailService _email;
    private readonly ILogger<AlertsService> _logger;

    public AlertsService(
        AppDbContext db,
        ITelegramService telegram,
        IEmailService email,
        ILogger<AlertsService> logger)
    {
        _db = db;
        _telegram = telegram;
        _email = email;
        _logger = logger;
    }

    private static string HtmlEscape(string? value)
        => string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>
    /// Проверить критические остатки товаров и отправить алерт
    /// </summary>
    public async Task CheckCriticalStocksAsync()
    {
        try
        {
            // Получаем товары с критическими остатками (<10 единиц)
            var criticalProducts = await (from stock in _db.Stocks
                                         group stock by stock.ProductId into g
                                         let totalQty = g.Sum(s => s.Qty)
                                         where totalQty > 0 && totalQty <= 10
                                         join product in _db.Products on g.Key equals product.Id
                                         select new
                                         {
                                             product.Name,
                                             product.Sku,
                                             Qty = totalQty
                                         })
                                         .OrderBy(p => p.Qty)
                                         .Take(10)
                                         .ToListAsync();

            if (criticalProducts.Any())
            {
                // Telegram алерт
                var message = "🔴 <b>КРИТИЧЕСКИЕ ОСТАТКИ!</b>\n\n";
                foreach (var p in criticalProducts)
                {
                    message += $"📦 {p.Name}\n";
                    message += $"   SKU: {p.Sku}\n";
                    message += $"   Остаток: <b>{p.Qty:F1}</b> ⚠️\n\n";
                }

                message += "Необходима срочная закупка!";
                await _telegram.SendMessageToOwnerAsync(message);
                
                // Email алерт
                var stockAlerts = criticalProducts.Select(p => new StockAlertDto
                {
                    ProductId = 0,
                    ProductName = p.Name,
                    CurrentStock = (int)p.Qty,
                    MinimumStock = 10,
                    WarehouseType = "Mixed"
                }).ToList();
                var emailHtml = EmailTemplates.CriticalStockAlert(stockAlerts);
                await _email.SendToOwnerAsync("🔴 КРИТИЧЕСКИЕ ОСТАТКИ ТОВАРОВ", emailHtml);
                
                _logger.LogInformation($"Алерт о критических остатках отправлен ({criticalProducts.Count} товаров) - Telegram + Email");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка проверки критических остатков");
        }
    }

    /// <summary>
    /// Проверить просроченные долги и отправить алерт
    /// </summary>
    public async Task CheckOverdueDebtsAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            var overdueDebts = await _db.Debts
                .Where(d => d.DueDate < now && d.Status == DebtStatus.Open)
                .Join(_db.Sales, d => d.SaleId, s => s.Id, (d, s) => new
                {
                    d.Id,
                    s.ClientName,
                    d.Amount,
                    d.DueDate,
                    DaysOverdue = (int)(now - d.DueDate).TotalDays
                })
                .OrderByDescending(d => d.DaysOverdue)
                .Take(10)
                .ToListAsync();

            if (overdueDebts.Any())
            {
                var totalOverdue = overdueDebts.Sum(d => d.Amount);
                var message = $"💸 <b>ПРОСРОЧЕННЫЕ ДОЛГИ!</b>\n\n";
                message += $"Всего: <b>{totalOverdue:N0} UZS</b>\n";
                message += $"Должников: <b>{overdueDebts.Count}</b>\n\n";

                foreach (var d in overdueDebts.Take(5))
                {
                    message += $"👤 {d.ClientName}\n";
                    message += $"   Сумма: {d.Amount:N0} UZS\n";
                    message += $"   Просрочка: <b>{d.DaysOverdue} дн.</b> ⏰\n\n";
                }

                if (overdueDebts.Count > 5)
                {
                    message += $"...и ещё {overdueDebts.Count - 5} должников\n\n";
                }

                message += "Необходимо взыскать долги!";

                await _telegram.SendMessageToOwnerAsync(message);
                _logger.LogInformation($"Алерт о просроченных долгах отправлен ({overdueDebts.Count} должников)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка проверки просроченных долгов");
        }
    }

    /// <summary>
    /// Проверить низкие балансы в кассах
    /// </summary>
    public async Task CheckLowCashboxBalancesAsync()
    {
        try
        {
            var lowBalanceCashboxes = await _db.Cashboxes
                .Where(c => c.IsActive && c.CurrentBalance < 1000000) // <1 млн UZS
                .OrderBy(c => c.CurrentBalance)
                .ToListAsync();

            if (lowBalanceCashboxes.Any())
            {
                var message = "💰 <b>НИЗКИЕ БАЛАНСЫ В КАССАХ!</b>\n\n";
                foreach (var cb in lowBalanceCashboxes)
                {
                    message += $"🏦 {cb.Name}\n";
                    message += $"   Баланс: <b>{cb.CurrentBalance:N0} {cb.Currency}</b> ⚠️\n\n";
                }
                message += "Рекомендуется пополнить кассы.";

                await _telegram.SendMessageToOwnerAsync(message);
                _logger.LogInformation($"Алерт о низких балансах отправлен ({lowBalanceCashboxes.Count} касс)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка проверки балансов касс");
        }
    }

    /// <summary>
    /// Проверить долгие брони без продажи
    /// </summary>
    public async Task CheckLongPendingReservationsAsync()
    {
        try
        {
            var twoDaysAgo = DateTime.UtcNow.AddDays(-2);
            var longReservations = await _db.Reservations
                .Where(r => r.Status == ReservationStatus.Active && r.CreatedAt < twoDaysAgo)
                .OrderBy(r => r.CreatedAt)
                .Take(10)
                .ToListAsync();

            if (longReservations.Any())
            {
                var message = "⏳ <b>ДОЛГИЕ БРОНИ БЕЗ ПРОДАЖИ!</b>\n\n";
                message += $"Бронирований: <b>{longReservations.Count}</b>\n\n";

                foreach (var r in longReservations.Take(5))
                {
                    var daysOld = (int)(DateTime.UtcNow - r.CreatedAt).TotalDays;
                    var clientName = "N/A";
                    if (r.ClientId.HasValue)
                    {
                        var client = await _db.Clients.FindAsync(r.ClientId.Value);
                        clientName = client?.Name ?? "N/A";
                    }
                    message += $"📋 Бронь #{r.Id}\n";
                    message += $"   Клиент: {clientName}\n";
                    message += $"   Создана: {daysOld} дн. назад\n";
                    message += $"   Срок: {r.ReservedUntil:dd.MM.yyyy}\n\n";
                }

                if (longReservations.Count > 5)
                {
                    message += $"...и ещё {longReservations.Count - 5} броней\n\n";
                }

                message += "Рекомендуется связаться с клиентами!";

                await _telegram.SendMessageToOwnerAsync(message);
                _logger.LogInformation($"Алерт о долгих бронях отправлен ({longReservations.Count} броней)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка проверки долгих броней");
        }
    }

    /// <summary>
    /// Отправить уведомление о крупной продаже
    /// </summary>
    public async Task NotifyLargeSaleAsync(int saleId, decimal total)
    {
        try
        {
            var sale = await _db.Sales
                .AsNoTracking()
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == saleId);

            if (sale == null) return;

            var clientName = string.IsNullOrWhiteSpace(sale.ClientName) ? "Посетитель" : sale.ClientName;
            var managerDisplay = string.IsNullOrWhiteSpace(sale.CreatedBy) ? "Не указан" : sale.CreatedBy;

            var items = sale.Items ?? new List<SaleItem>();
            var productIds = items.Select(i => i.ProductId).Distinct().ToList();
            var productMap = await _db.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name })
                .ToDictionaryAsync(p => p.Id, p => p.Name, CancellationToken.None);

            var itemLines = new List<string>();
            foreach (var it in items)
            {
                productMap.TryGetValue(it.ProductId, out var productName);
                var name = productName ?? $"#{it.ProductId}";
                var sum = it.Qty * it.UnitPrice;
                var nameShort = name.Length > 28 ? name[..28] + "…" : name;
                var safeName = HtmlEscape(nameShort);
                itemLines.Add($"{safeName,-30} {it.Qty,5:N0} x {it.UnitPrice,9:N0} = {sum,10:N0}");
            }

            var message = $"🔥 <b>КРУПНАЯ ПРОДАЖА #{sale.Id}!</b> 🔥\n";
            message += $"📅 Дата: {sale.CreatedAt.AddMinutes(300):yyyy-MM-dd HH:mm}\n";
            message += $"👤 Клиент: {HtmlEscape(clientName)}\n";
            message += $"💳 Оплата: {sale.PaymentType}\n";
            message += $"📦 Позиции: {items.Count} (шт: {items.Sum(i => i.Qty):N0})\n";
            message += $"💰 Итого: <b>{total:N0} UZS</b>\n";
            message += $"👨‍💼 Менеджер: {HtmlEscape(managerDisplay)}";

            if (itemLines.Count > 0)
            {
                message += "\n<pre>" + string.Join("\n", itemLines) + "</pre>";
            }

            message += "\n\n✅ Крупная сделка требует внимания!";

            await _telegram.SendMessageToOwnerAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Ошибка отправки уведомления о крупной продаже {saleId}");
        }
    }
}
