using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Integrations.Telegram;
using System.Text.Json;
using System.Linq;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TelegramController(AppDbContext db, ITelegramService tg, IOptions<TelegramSettings> options) : ControllerBase
{
    private readonly TelegramSettings _settings = options.Value;

    // POST /api/telegram/webhook
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        // Optional secret token validation
        if (Request.Headers.TryGetValue("X-Telegram-Bot-Api-Secret-Token", out var header) && !string.IsNullOrWhiteSpace(_settings.SecretToken))
        {
            if (!string.Equals(header.ToString(), _settings.SecretToken, StringComparison.Ordinal))
                return Unauthorized();
        }

        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var message = root.GetProperty("message");
        var chatId = message.GetProperty("chat").GetProperty("id").GetInt64();
        var text = message.TryGetProperty("text", out var t) ? t.GetString() ?? string.Empty : string.Empty;

        if (string.IsNullOrWhiteSpace(text)) return Ok();

        if (text.StartsWith("/start") || text.StartsWith("/help"))
        {
            var kb = new
            {
                keyboard = new object[]
                {
                    new object[] { new { text = "/report today" }, new { text = "/top today" } },
                    new object[] { new { text = "/report week" }, new { text = "/top week" } },
                    new object[] { new { text = "/report month" }, new { text = "/top month" } }
                },
                resize_keyboard = true,
                one_time_keyboard = false
            };
            await tg.SendMessageAsync(chatId,
                "Добро пожаловать! Доступные команды:\n/report today|week|month — отчёт по продажам\n/top today|week|month — топ-1 продавец\n/stock <SKU> — остатки по артикулу\n/whoami — показать ваш chat id",
                kb,
                HttpContext.RequestAborted);
            return Ok();
        }

        if (text.StartsWith("/whoami", StringComparison.OrdinalIgnoreCase))
        {
            await tg.SendMessageAsync(chatId, $"Ваш chat id: {chatId}. Добавьте его в переменную PROJECTAPP__Telegram__AllowedChatIds, чтобы получать уведомления о продажах.", HttpContext.RequestAborted);
            return Ok();
        }

        if (text.StartsWith("/report", StringComparison.OrdinalIgnoreCase))
        {
            // Usage: /report today|week|month
            var preset = "today";
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2) preset = parts[1].ToLowerInvariant();

            (DateTime from, DateTime to) = ResolveRange(preset);

            var rows = await db.Sales
                .AsNoTracking()
                .Where(s => s.CreatedAt >= from && s.CreatedAt < to)
                .Select(s => new { s.Total, s.CreatedBy, Qty = s.Items.Sum(i => i.Qty) })
                .ToListAsync(HttpContext.RequestAborted);

            var totalAmount = rows.Sum(r => r.Total);
            var totalQty = rows.Sum(r => r.Qty);
            var salesCount = rows.Count;
            var top = rows
                .GroupBy(r => r.CreatedBy ?? "unknown")
                .Select(g => new { Seller = g.Key, Amount = g.Sum(x => x.Total) })
                .OrderByDescending(x => x.Amount)
                .FirstOrDefault();

            var title = preset switch
            {
                "week" => "Отчёт за неделю",
                "month" => "Отчёт за месяц",
                _ => "Отчёт за сегодня"
            };

            var msg = $"{title}\nПериод: {from:yyyy-MM-dd}..{to:yyyy-MM-dd}\nОборот: {totalAmount}\nШтук: {totalQty}\nЧеки: {salesCount}\nТоп продавец: {top?.Seller ?? "нет"} ({top?.Amount ?? 0m})";
            await tg.SendMessageAsync(chatId, msg, HttpContext.RequestAborted);
            return Ok();
        }

        if (text.StartsWith("/top", StringComparison.OrdinalIgnoreCase))
        {
            // Usage: /top today|week|month
            var preset = "today";
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2) preset = parts[1].ToLowerInvariant();

            (DateTime from, DateTime to) = ResolveRange(preset);

            var rows = await db.Sales
                .AsNoTracking()
                .Where(s => s.CreatedAt >= from && s.CreatedAt < to)
                .Select(s => new { s.Total, s.CreatedBy })
                .ToListAsync(HttpContext.RequestAborted);

            var top = rows
                .GroupBy(r => r.CreatedBy ?? "unknown")
                .Select(g => new { Seller = g.Key, Amount = g.Sum(x => x.Total) })
                .OrderByDescending(x => x.Amount)
                .FirstOrDefault();

            var title = preset switch { "week" => "ТОП за неделю", "month" => "ТОП за месяц", _ => "ТОП за сегодня" };
            var msg = top is null
                ? $"{title}: данных нет"
                : $"{title}: {top.Seller} — {top.Amount}";

            await tg.SendMessageAsync(chatId, msg, HttpContext.RequestAborted);
            return Ok();
        }

        if (text.StartsWith("/stock", StringComparison.OrdinalIgnoreCase))
        {
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                await tg.SendMessageAsync(chatId, "Использование: /stock SKU-001", HttpContext.RequestAborted);
                return Ok();
            }
            var sku = parts[1];
            // Simple stock query via DB (view-less)
            var qtys = from p in db.Products
                       where p.Sku == sku
                       join s in db.Stocks on p.Id equals s.ProductId
                       group s by p.Sku into g
                       select new
                       {
                           Total = g.Where(x => x.Register == Models.StockRegister.IM40 || x.Register == Models.StockRegister.ND40).Sum(x => x.Qty),
                           IM40 = g.Where(x => x.Register == Models.StockRegister.IM40).Sum(x => x.Qty),
                           ND40 = g.Where(x => x.Register == Models.StockRegister.ND40).Sum(x => x.Qty)
                       };
            var res = await qtys.FirstOrDefaultAsync();
            if (res is null)
                await tg.SendMessageAsync(chatId, $"SKU {sku} не найден", HttpContext.RequestAborted);
            else
                await tg.SendMessageAsync(chatId, $"{sku}: всего={res.Total}, IM40={res.IM40}, ND40={res.ND40}", HttpContext.RequestAborted);
            return Ok();
        }

        await tg.SendMessageAsync(chatId, "Неизвестная команда. /help", HttpContext.RequestAborted);
        return Ok();
    }

    private static (DateTime From, DateTime To) ResolveRange(string preset)
    {
        var now = DateTime.UtcNow;
        switch (preset)
        {
            case "week":
                var monday = StartOfWeekUtc(now);
                return (monday, monday.AddDays(7));
            case "month":
                var first = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                return (first, first.AddMonths(1));
            default:
                var start = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
                return (start, start.AddDays(1));
        }
    }

    private static DateTime StartOfWeekUtc(DateTime dt)
    {
        var d = dt.Date;
        int diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(-diff);
    }

    // POST /api/telegram/set-webhook
    [HttpPost("set-webhook")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SetWebhook()
    {
        if (string.IsNullOrWhiteSpace(_settings.PublicUrl))
            return ValidationProblem("Telegram:PublicUrl is not configured");
        var url = $"{_settings.PublicUrl.TrimEnd('/')}/api/telegram/webhook";
        var ok = await tg.SetWebhookAsync(url, _settings.SecretToken, HttpContext.RequestAborted);
        return Ok(new { ok, url });
    }

    // POST /api/telegram/delete-webhook
    [HttpPost("delete-webhook")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteWebhook()
    {
        var ok = await tg.DeleteWebhookAsync(HttpContext.RequestAborted);
        return Ok(new { ok });
    }
}
