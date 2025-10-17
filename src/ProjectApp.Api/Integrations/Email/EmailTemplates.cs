using ProjectApp.Api.Services;

namespace ProjectApp.Api.Integrations.Email;

/// <summary>
/// HTML-шаблоны для Email-уведомлений
/// </summary>
public static class EmailTemplates
{
    /// <summary>
    /// Базовый HTML-шаблон с header и footer
    /// </summary>
    private static string BaseTemplate(string title, string content)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; background-color: #f4f4f4; }}
        .container {{ max-width: 600px; margin: 20px auto; background: #fff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: #fff; padding: 30px; text-align: center; }}
        .header h1 {{ margin: 0; font-size: 24px; }}
        .content {{ padding: 30px; }}
        .footer {{ background: #f8f9fa; padding: 20px; text-align: center; font-size: 12px; color: #666; }}
        .metric-box {{ background: #f8f9fa; padding: 15px; margin: 10px 0; border-radius: 6px; border-left: 4px solid #667eea; }}
        .metric-label {{ font-size: 12px; color: #666; text-transform: uppercase; }}
        .metric-value {{ font-size: 24px; font-weight: bold; color: #333; margin: 5px 0; }}
        .alert-box {{ background: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 15px 0; border-radius: 6px; }}
        .success-box {{ background: #d4edda; border-left: 4px solid #28a745; padding: 15px; margin: 15px 0; border-radius: 6px; }}
        .danger-box {{ background: #f8d7da; border-left: 4px solid #dc3545; padding: 15px; margin: 15px 0; border-radius: 6px; }}
        table {{ width: 100%; border-collapse: collapse; margin: 15px 0; }}
        th {{ background: #f8f9fa; padding: 10px; text-align: left; font-size: 12px; text-transform: uppercase; color: #666; }}
        td {{ padding: 10px; border-bottom: 1px solid #e9ecef; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🔥 {title}</h1>
        </div>
        <div class='content'>
            {content}
        </div>
        <div class='footer'>
            <p>Отчет сгенерирован автоматически<br>
            <strong>ProjectApp</strong> - Система управления бизнесом</p>
            <p>© 2025 ProjectApp. Все права защищены.</p>
        </div>
    </div>
</body>
</html>";
    }

    /// <summary>
    /// Ежедневный отчет
    /// </summary>
    public static string DailyReport(OwnerDashboardDto dashboard)
    {
        var content = $@"
<h2>📊 Ежедневный отчет за {dashboard.GeneratedAt:dd MMMM yyyy}</h2>

<div class='metric-box'>
    <div class='metric-label'>💰 Финансы за сегодня</div>
    <table>
        <tr>
            <td>Выручка:</td>
            <td style='text-align: right;'><strong>{dashboard.TodayRevenue:N0} UZS</strong></td>
        </tr>
        <tr>
            <td>Прибыль:</td>
            <td style='text-align: right;'><strong>{dashboard.TodayProfit:N0} UZS</strong></td>
        </tr>
        <tr>
            <td>Продаж:</td>
            <td style='text-align: right;'><strong>{dashboard.TodaySalesCount}</strong></td>
        </tr>
        <tr>
            <td>Средний чек:</td>
            <td style='text-align: right;'><strong>{dashboard.TodayAverageCheck:N0} UZS</strong></td>
        </tr>
    </table>
</div>

<div class='metric-box'>
    <div class='metric-label'>💵 Остатки в кассах</div>
    <table>
        {string.Join("", dashboard.CashboxBalances.Select(cb => $"<tr><td>{cb.Key}:</td><td style='text-align: right;'><strong>{cb.Value:N0} UZS</strong></td></tr>"))}
        <tr style='background: #f8f9fa;'>
            <td><strong>Всего:</strong></td>
            <td style='text-align: right;'><strong>{dashboard.TotalCash:N0} UZS</strong></td>
        </tr>
    </table>
</div>

<div class='metric-box'>
    <div class='metric-label'>📦 Склад</div>
    <table>
        <tr>
            <td>Стоимость товара:</td>
            <td style='text-align: right;'><strong>{dashboard.InventoryValue:N0} UZS</strong></td>
        </tr>
        <tr>
            <td>Критических остатков:</td>
            <td style='text-align: right;'><strong>{dashboard.CriticalStockAlerts.Count}</strong> {(dashboard.CriticalStockAlerts.Any() ? "⚠️" : "✅")}</td>
        </tr>
    </table>
</div>

<div class='metric-box'>
    <div class='metric-label'>💸 Долги</div>
    <table>
        <tr>
            <td>Клиенты должны:</td>
            <td style='text-align: right;'><strong>{dashboard.ClientDebts:N0} UZS</strong></td>
        </tr>
        <tr>
            <td>Просроченных:</td>
            <td style='text-align: right;'><strong>{dashboard.OverdueDebts.Count}</strong> {(dashboard.OverdueDebts.Any() ? "⚠️" : "✅")}</td>
        </tr>
    </table>
</div>

{(dashboard.Top5ProductsToday.Any() ? $@"
<h3>🏆 ТОП-5 ТОВАРОВ ДНЯ</h3>
<table>
    <thead>
        <tr>
            <th>#</th>
            <th>Товар</th>
            <th>Выручка</th>
            <th>Кол-во</th>
        </tr>
    </thead>
    <tbody>
        {string.Join("", dashboard.Top5ProductsToday.Take(5).Select((p, i) => $@"
        <tr>
            <td>{i + 1}</td>
            <td>{p.ProductName}</td>
            <td style='text-align: right;'>{p.TotalRevenue:N0} UZS</td>
            <td style='text-align: right;'>{p.TotalQuantity} шт</td>
        </tr>"))}
    </tbody>
</table>" : "<p>🏆 <strong>ТОП-5 ТОВАРОВ:</strong> нет продаж за сегодня</p>")}
";

        return BaseTemplate("Ежедневный отчет", content);
    }

    /// <summary>
    /// Крупная продажа
    /// </summary>
    public static string LargeSaleAlert(int saleId, string clientName, decimal total, string managerName, int itemsCount)
    {
        var content = $@"
<div class='success-box'>
    <h2>🔥 КРУПНАЯ ПРОДАЖА!</h2>
    <p>Зафиксирована крупная продажа на сумму <strong>{total:N0} UZS</strong>!</p>
</div>

<table>
    <tr>
        <td>📊 Продажа:</td>
        <td><strong>#{saleId}</strong></td>
    </tr>
    <tr>
        <td>👤 Клиент:</td>
        <td><strong>{clientName}</strong></td>
    </tr>
    <tr>
        <td>💰 Сумма:</td>
        <td><strong>{total:N0} UZS</strong></td>
    </tr>
    <tr>
        <td>👨‍💼 Менеджер:</td>
        <td><strong>{managerName}</strong></td>
    </tr>
    <tr>
        <td>📦 Товаров:</td>
        <td><strong>{itemsCount} позиций</strong></td>
    </tr>
    <tr>
        <td>🕐 Время:</td>
        <td><strong>{DateTime.UtcNow:dd.MM.yyyy HH:mm}</strong></td>
    </tr>
</table>

<p style='text-align: center; margin-top: 20px; font-size: 18px; color: #28a745;'>
    <strong>Отличная работа! 🎉</strong>
</p>
";

        return BaseTemplate("Крупная продажа", content);
    }

    /// <summary>
    /// Критические остатки товаров
    /// </summary>
    public static string CriticalStockAlert(List<StockAlertDto> alerts)
    {
        var content = $@"
<div class='danger-box'>
    <h2>🔴 КРИТИЧЕСКИЕ ОСТАТКИ ТОВАРОВ!</h2>
    <p>Обнаружено <strong>{alerts.Count}</strong> товаров с критически низкими остатками.</p>
</div>

<table>
    <thead>
        <tr>
            <th>Товар</th>
            <th>Остаток</th>
            <th>Минимум</th>
            <th>Склад</th>
        </tr>
    </thead>
    <tbody>
        {string.Join("", alerts.Take(10).Select(a => $@"
        <tr>
            <td>{a.ProductName}</td>
            <td style='text-align: right; color: #dc3545;'><strong>{a.CurrentStock} шт</strong></td>
            <td style='text-align: right;'>{a.MinimumStock} шт</td>
            <td>{a.WarehouseType}</td>
        </tr>"))}
    </tbody>
</table>

{(alerts.Count > 10 ? $"<p><em>...и ещё {alerts.Count - 10} товаров</em></p>" : "")}

<div class='alert-box'>
    <p><strong>⚠️ Необходимо срочно пополнить склад!</strong></p>
</div>
";

        return BaseTemplate("Критические остатки", content);
    }

    /// <summary>
    /// Просроченные долги
    /// </summary>
    public static string OverdueDebtsAlert(List<OverdueDebtDto> debts)
    {
        var totalAmount = debts.Sum(d => d.Amount);
        
        var content = $@"
<div class='danger-box'>
    <h2>💸 ПРОСРОЧЕННЫЕ ДОЛГИ КЛИЕНТОВ!</h2>
    <p>Обнаружено <strong>{debts.Count}</strong> просроченных долгов на общую сумму <strong>{totalAmount:N0} UZS</strong>.</p>
</div>

<table>
    <thead>
        <tr>
            <th>Клиент</th>
            <th>Сумма</th>
            <th>Просрочка</th>
        </tr>
    </thead>
    <tbody>
        {string.Join("", debts.Take(10).Select(d => $@"
        <tr>
            <td>{d.ClientName}</td>
            <td style='text-align: right;'><strong>{d.Amount:N0} UZS</strong></td>
            <td style='text-align: right; color: #dc3545;'><strong>{d.DaysOverdue} дней</strong></td>
        </tr>"))}
    </tbody>
</table>

{(debts.Count > 10 ? $"<p><em>...и ещё {debts.Count - 10} долгов</em></p>" : "")}

<div class='alert-box'>
    <p><strong>⚠️ Необходимо взыскать долги!</strong></p>
</div>
";

        return BaseTemplate("Просроченные долги", content);
    }

    /// <summary>
    /// Низкие балансы в кассах
    /// </summary>
    public static string LowCashBalanceAlert(Dictionary<string, decimal> lowBalances)
    {
        var content = $@"
<div class='alert-box'>
    <h2>💰 НИЗКИЕ БАЛАНСЫ В КАССАХ!</h2>
    <p>Обнаружены кассы с балансом менее 1,000,000 UZS.</p>
</div>

<table>
    <thead>
        <tr>
            <th>Касса</th>
            <th>Баланс</th>
        </tr>
    </thead>
    <tbody>
        {string.Join("", lowBalances.Select(cb => $@"
        <tr>
            <td>{cb.Key}</td>
            <td style='text-align: right; color: #ffc107;'><strong>{cb.Value:N0} UZS</strong></td>
        </tr>"))}
    </tbody>
</table>

<div class='alert-box'>
    <p><strong>⚠️ Рекомендуется пополнить кассы.</strong></p>
</div>
";

        return BaseTemplate("Низкие балансы в кассах", content);
    }
}
