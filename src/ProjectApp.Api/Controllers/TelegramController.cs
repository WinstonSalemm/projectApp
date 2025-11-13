using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Integrations.Telegram;
using ProjectApp.Api.Services;
using System.Text.Json;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TelegramController(AppDbContext db, ITelegramService tg, IOptions<TelegramSettings> options, AutoReportsService reports) : ControllerBase
{
    private readonly TelegramSettings _settings = options.Value;
    private readonly AutoReportsService _reports = reports;

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

        try
        {
            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Support both "message" and "edited_message"; ignore other update types
            JsonElement message;
            if (!root.TryGetProperty("message", out message))
            {
                if (root.TryGetProperty("edited_message", out var edited))
                    message = edited;
                else
                    return Ok();
            }

            if (!message.TryGetProperty("chat", out var chatElem) || !chatElem.TryGetProperty("id", out var idElem))
                return Ok();
            var chatId = idElem.GetInt64();
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
                        new object[] { new { text = "/report month" }, new { text = "/top month" } },
                        new object[] { new { text = "/report excel today" }, new { text = "/report excel week" } },
                        new object[] { new { text = "/reportfull today" }, new { text = "/reportfull week" } },
                        new object[] { new { text = "/stockall" } }
                    },
                    resize_keyboard = true,
                    one_time_keyboard = false
                };
                await tg.SendMessageAsync(chatId,
                    "Добро пожаловать! Доступные команды:\n" +
                    "/report today|week|month — отчёт по продажам\n" +
                    "/top today|week|month — топ-1 продавец\n" +
                    "/stock <SKU> — остатки по артикулу\n" +
                    "/report excel today|week|month — Excel-отчёт (подробный, с возвратами и долгами)\n" +
                    "/reportfull today|week|month — развернутый отчёт (товары и менеджеры)\n" +
                    "/stockall — список остатков по всем товарам\n" +
                    "/whoami — показать ваш chat id",
                    kb,
                    HttpContext.RequestAborted);
                return Ok();
            }

        if (text.StartsWith("/whoami", StringComparison.OrdinalIgnoreCase))
        {
            await tg.SendMessageAsync(chatId, $"Ваш chat id: {chatId}. Добавьте его в переменную PROJECTAPP__Telegram__AllowedChatIds, чтобы получать уведомления о продажах.", HttpContext.RequestAborted);
            return Ok();
        }

        if (text.StartsWith("/report excel", StringComparison.OrdinalIgnoreCase))
        {
            // Usage: /report excel today|week|month (uses local time by Telegram settings offset)
            var preset = "today";
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 3) preset = parts[2].ToLowerInvariant();

            var offset = TimeSpan.FromMinutes(_settings.TimeZoneOffsetMinutes);
            var nowUtc = DateTime.UtcNow;
            DateTime fromUtc;
            DateTime toUtc;
            switch (preset)
            {
                case "week":
                    var localNow = nowUtc + offset;
                    var localMonday = StartOfWeekUtc(localNow) + TimeSpan.Zero; // returns 00:00 local Monday but as UTC-kind value; we'll reconstruct
                    var mondayLocal = new DateTime(localNow.Year, localNow.Month, localNow.Day, 0, 0, 0, DateTimeKind.Unspecified);
                    // Recompute Monday correctly:
                    int diff = (7 + (int)mondayLocal.DayOfWeek - (int)DayOfWeek.Monday) % 7;
                    var weekStartLocal = mondayLocal.AddDays(-diff);
                    fromUtc = weekStartLocal - offset;
                    toUtc = fromUtc.AddDays(7);
                    break;
                case "month":
                    var ln = nowUtc + offset;
                    var firstLocal = new DateTime(ln.Year, ln.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
                    fromUtc = firstLocal - offset;
                    toUtc = fromUtc.AddMonths(1);
                    break;
                default:
                    var todayLocal = (nowUtc + offset).Date; // 00:00 local
                    fromUtc = todayLocal - offset;
                    toUtc = fromUtc.AddDays(1);
                    break;
            }

            await tg.SendMessageAsync(chatId, "⏳ Формирую Excel-отчёт...", HttpContext.RequestAborted);
            await _reports.SendExcelPeriodReportAsync(fromUtc, toUtc, chatId);
            return Ok();
        }

        if (text.StartsWith("/reportfull", StringComparison.OrdinalIgnoreCase))
        {
            // Usage: /reportfull today|week|month
            var preset = "today";
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2) preset = parts[1].ToLowerInvariant();

            (DateTime from, DateTime to) = ResolveRange(preset);
            await tg.SendMessageAsync(chatId, "⏳ Формирую развернутый отчёт...", HttpContext.RequestAborted);
            await _reports.SendDetailedPeriodReportAsync(from, to, chatId);
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

        if (text.StartsWith("/stockall", StringComparison.OrdinalIgnoreCase))
        {
            await tg.SendMessageAsync(chatId, "⏳ Готовлю список остатков...", HttpContext.RequestAborted);
            await _reports.SendEndOfDayStockAsync(chatId);
            return Ok();
        }

        await tg.SendMessageAsync(chatId, "Неизвестная команда. /help", HttpContext.RequestAborted);
        return Ok();
        }
        catch
        {
            // Never fail the webhook; just acknowledge to stop retries
            return Ok();
        }
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

    // GET /api/telegram/webhook-info
    [HttpGet("webhook-info")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetWebhookInfo()
    {
        var info = await tg.GetWebhookInfoAsync(HttpContext.RequestAborted);
        return Content(info, "application/json");
    }

    public record SendRequest(long ChatId, string? Text);

    // POST /api/telegram/send
    [HttpPost("send")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Send([FromBody] SendRequest req)
    {
        var (ok, body, status) = await tg.SendMessageDebugAsync(req.ChatId, string.IsNullOrWhiteSpace(req.Text) ? "ping" : req.Text!, null, HttpContext.RequestAborted);
        return Ok(new { ok, status, body });
    }

    public record SendReportNowRequest(List<string>? Files);

    // POST /api/telegram/send-report-now
    [HttpPost("send-report-now")]
    [Authorize(Policy = "AdminOnly")]
    [Consumes("application/json")]
    public async Task<IActionResult> SendReportNow([FromBody] SendReportNowRequest req)
    {
        var ids = _settings.ParseAllowedChatIds();
        if (ids.Count == 0) return ValidationProblem("Telegram AllowedChatIds not configured");

        // Build today's summary (local day based on settings offset)
        var offset = TimeSpan.FromMinutes(_settings.TimeZoneOffsetMinutes);
        var nowUtc = DateTime.UtcNow;
        var localToday = (nowUtc + offset).Date; // 00:00 local
        var fromUtc = localToday - offset;
        var toUtc = localToday.AddDays(1) - offset;

        var rows = await db.Sales
            .AsNoTracking()
            .Where(s => s.CreatedAt >= fromUtc && s.CreatedAt < toUtc)
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

        var periodStr = localToday.ToString("yyyy-MM-dd");
        var msg = $"Ежедневная сводка за {periodStr}\nОборот: {totalAmount}\nШтук: {totalQty}\nЧеки: {salesCount}\nТоп продавец: {top?.Seller ?? "нет"} ({top?.Amount ?? 0m})";

        foreach (var chatId in ids)
        {
            try { await tg.SendMessageAsync(chatId, msg, HttpContext.RequestAborted); } catch { }
        }

        // Optionally send provided photos from file system (absolute paths recommended)
        if (req?.Files != null && req.Files.Count > 0)
        {
            foreach (var filePath in req.Files)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(filePath)) continue;
                    if (!System.IO.File.Exists(filePath)) continue;
                    await using var fs = System.IO.File.OpenRead(filePath);
                    foreach (var chatId in ids)
                    {
                        fs.Position = 0;
                        try { await tg.SendPhotoAsync(chatId, fs, System.IO.Path.GetFileName(filePath), null, null, HttpContext.RequestAborted); } catch { }
                    }
                }
                catch { }
            }
        }

        return Ok(new { ok = true });
    }

    // POST /api/telegram/send-report-now-multipart (multipart form-data with files)
    [HttpPost("send-report-now-multipart")]
    [Authorize(Policy = "AdminOnly")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SendReportNowMultipart([FromForm(Name = "files")] List<IFormFile>? files)
    {
        var ids = _settings.ParseAllowedChatIds();
        if (ids.Count == 0) return ValidationProblem("Telegram AllowedChatIds not configured");

        // Build today's summary (local day based on settings offset)
        var offset = TimeSpan.FromMinutes(_settings.TimeZoneOffsetMinutes);
        var nowUtc = DateTime.UtcNow;
        var localToday = (nowUtc + offset).Date; // 00:00 local
        var fromUtc = localToday - offset;
        var toUtc = localToday.AddDays(1) - offset;

        var rows = await db.Sales
            .AsNoTracking()
            .Where(s => s.CreatedAt >= fromUtc && s.CreatedAt < toUtc)
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

        var periodStr = localToday.ToString("yyyy-MM-dd");
        var msg = $"Ежедневная сводка за {periodStr}\nОборот: {totalAmount}\nШтук: {totalQty}\nЧеки: {salesCount}\nТоп продавец: {top?.Seller ?? "нет"} ({top?.Amount ?? 0m})";
        foreach (var chatId in ids)
        {
            try { await tg.SendMessageAsync(chatId, msg, HttpContext.RequestAborted); } catch { }
        }

        int sent = 0;
        if (files != null && files.Count > 0)
        {
            foreach (var f in files.Where(f => f.Length > 0))
            {
                await using var stream = f.OpenReadStream();
                foreach (var chatId in ids)
                {
                    stream.Position = 0;
                    try { await tg.SendPhotoAsync(chatId, stream, f.FileName ?? "photo.jpg", null, null, HttpContext.RequestAborted); } catch { }
                }
                sent++;
            }
        }

        return Ok(new { ok = true, files = sent });
    }
}
