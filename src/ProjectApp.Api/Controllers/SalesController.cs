using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Models;
using ProjectApp.Api.Repositories;
using ProjectApp.Api.Services;
using ProjectApp.Api.Integrations.Telegram;
using Microsoft.Extensions.Options;
using ProjectApp.Api.Data;
using System.IO;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SalesController : ControllerBase
{
    private readonly ISaleRepository _sales;
    private readonly ISaleCalculator _calculator;
    private readonly ILogger<SalesController> _logger;
    private readonly ISalesNotifier _notifier;
    private readonly ITelegramService _tg;
    private readonly TelegramSettings _tgSettings;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly CommissionService _commissionService;

    public SalesController(ISaleRepository sales, ISaleCalculator calculator, ILogger<SalesController> logger, ISalesNotifier notifier, ITelegramService tg, IOptions<TelegramSettings> tgOptions, AppDbContext db, IConfiguration config, CommissionService commissionService)
    {
        _sales = sales;
        _calculator = calculator;
        _logger = logger;
        _notifier = notifier;
        _tg = tg;
        _tgSettings = tgOptions.Value;
        _db = db;
        _config = config;
        _commissionService = commissionService;
    }

    [HttpGet]
    [Authorize(Policy = "ManagerOnly")] // Admin or Manager
    [ProducesResponseType(typeof(IEnumerable<Sale>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo, [FromQuery] string? createdBy, [FromQuery] string? paymentType, [FromQuery] int? clientId, [FromQuery] bool all = false, CancellationToken ct = default)
    {
        var isAdmin = User.IsInRole("Admin");
        var allowAll = isAdmin || all;
        var effectiveCreatedBy = allowAll ? createdBy : (User?.Identity?.Name ?? createdBy);
        var list = await _sales.QueryAsync(dateFrom, dateTo, effectiveCreatedBy, paymentType, clientId, ct);
        return Ok(list);
    }

    [HttpPost]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(Sale), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] SaleCreateDto dto, CancellationToken ct)
    {
        try
        {
            var sale = await _calculator.BuildAndCalculateAsync(dto, ct);
            sale.CreatedBy = User?.Identity?.Name;
            sale = await _sales.AddAsync(sale, ct);

            _logger.LogInformation("Sale created {SaleId} for client {ClientId} total {Total} payment {PaymentType}",
                sale.Id, sale.ClientId, sale.Total, sale.PaymentType);

            // КОМИССИЯ ПАРТНЕРУ: Если указан партнер и % комиссии - начисляем
            if (sale.CommissionAgentId.HasValue && sale.CommissionRate.HasValue && sale.CommissionRate > 0)
            {
                var commissionAmount = Math.Round(sale.Total * sale.CommissionRate.Value / 100, 2);
                sale.CommissionAmount = commissionAmount;
                
                // Начисляем комиссию партнеру
                _ = Task.Run(async () => 
                {
                    try
                    {
                        await _commissionService.AccrueCommissionForSaleAsync(
                            sale.Id,
                            sale.CommissionAgentId.Value,
                            sale.Total,
                            sale.CommissionRate.Value,
                            sale.CreatedBy
                        );
                        _logger.LogInformation("Начислена комиссия {Amount} партнеру {AgentId} за продажу {SaleId}",
                            commissionAmount, sale.CommissionAgentId.Value, sale.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка начисления комиссии за продажу {SaleId}", sale.Id);
                    }
                }, CancellationToken.None);
            }

            // СОЗДАНИЕ ДОЛГА: Если тип оплаты = Debt - создаем запись о долге
            if (sale.PaymentType == PaymentType.Debt)
            {
                if (!sale.ClientId.HasValue)
                {
                    return ValidationProblem("Для продажи в долг необходимо указать клиента");
                }

                var dueDate = dto.DebtDueDate ?? DateTime.UtcNow.AddDays(30); // По умолчанию 30 дней
                
                var debt = new Debt
                {
                    ClientId = sale.ClientId.Value,
                    SaleId = sale.Id,
                    Amount = sale.Total,
                    OriginalAmount = sale.Total,
                    DueDate = dueDate,
                    Status = DebtStatus.Open,
                    Notes = dto.DebtNotes,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = sale.CreatedBy,
                    Items = sale.Items.Select(si => new DebtItem
                    {
                        ProductId = si.ProductId,
                        ProductName = si.ProductName,
                        Sku = si.Sku,
                        Qty = si.Qty,
                        Price = si.Price,
                        Total = si.Total,
                        CreatedAt = DateTime.UtcNow
                    }).ToList()
                };

                _db.Debts.Add(debt);
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("Создан долг {DebtId} для клиента {ClientId} на сумму {Amount}, срок {DueDate}",
                    debt.Id, debt.ClientId, debt.Amount, debt.DueDate);
            }

            // Notification: if client set NotifyHold=true, skip text; Android client will upload photo+caption
            if (!(dto.NotifyHold ?? false))
            {
                // Fire-and-forget notification (do not block the response)
                // Use CancellationToken.None to avoid request-abort cancelling Telegram send
                _ = _notifier.NotifySaleAsync(sale, CancellationToken.None);
            }

            var location = $"/api/sales/{sale.Id}";
            return Created(location, sale);
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(detail: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ValidationProblem(detail: ex.Message);
        }
        catch (DbUpdateException ex)
        {
            // Surface DB constraint issues (e.g., identity/keys/length) to caller as 400 for faster diagnosis
            var msg = ex.InnerException?.Message ?? ex.Message;
            return ValidationProblem(detail: msg);
        }
        catch (Exception ex)
        {
            // As a temporary measure, return details to help diagnose persistent 500s
            var msg = ex.InnerException?.Message ?? ex.Message;
            _logger.LogError(ex, "Error creating sale");
            return ValidationProblem(detail: msg);
        }
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Sale), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken ct)
    {
        var sale = await _sales.GetByIdAsync(id, ct);
        if (sale is null) return NotFound();
        return Ok(sale);
    }

    private static string HtmlEscape(string? s)
        => string.IsNullOrEmpty(s)
            ? string.Empty
            : s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string PaymentTypeRu(PaymentType pt) => pt switch
    {
        PaymentType.CashWithReceipt => "Наличные (чек)",
        PaymentType.CardWithReceipt => "Карта (чек)",
        PaymentType.ClickWithReceipt => "Click (чек)",
        PaymentType.CashNoReceipt => "Наличные (без чека)",
        PaymentType.ClickNoReceipt => "Click (без чека)",
        PaymentType.Click => "Click",
        PaymentType.Payme => "Payme",
        PaymentType.Site => "Сайт",
        PaymentType.Reservation => "Резервация",
        PaymentType.Return => "Возврат",
        PaymentType.Contract => "По договору",
        _ => pt.ToString()
    };

    private async Task<string> BuildSaleCaptionAsync(Sale sale, CancellationToken ct)
    {
        var localTime = sale.CreatedAt.AddMinutes(_tgSettings.TimeZoneOffsetMinutes);
        var clientName = string.IsNullOrWhiteSpace(sale.ClientName) ? "Посетитель" : sale.ClientName;
        var createdBy = sale.CreatedBy ?? string.Empty;
        string managerDisplay = createdBy;
        try
        {
            if (!string.IsNullOrWhiteSpace(createdBy))
            {
                var dbUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserName == createdBy, ct);
                if (dbUser is not null && !string.IsNullOrWhiteSpace(dbUser.DisplayName))
                    managerDisplay = dbUser.DisplayName;
                else
                {
                    var cfgUsers = _config.GetSection("Users").Get<List<dynamic>>();
                    if (cfgUsers is not null)
                    {
                        foreach (var u in cfgUsers)
                        {
                            try
                            {
                                string uname = u.UserName;
                                if (string.Equals(uname, createdBy, StringComparison.OrdinalIgnoreCase))
                                {
                                    managerDisplay = string.IsNullOrWhiteSpace((string?)u.DisplayName) ? uname : (string)u.DisplayName;
                                    break;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
        }
        catch { }
        if (string.IsNullOrWhiteSpace(managerDisplay)) managerDisplay = "n/a";

        var pids = sale.Items?.Select(i => i.ProductId).Distinct().ToList() ?? new List<int>();
        var prodMap = await _db.Products.AsNoTracking()
            .Where(p => pids.Contains(p.Id))
            .Select(p => new { p.Id, p.Sku, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p, ct);

        var lines = new List<string>();
        foreach (var it in sale.Items ?? new List<SaleItem>())
        {
            prodMap.TryGetValue(it.ProductId, out var p);
            var name = p?.Name ?? $"#{it.ProductId}";
            var sum = it.Qty * it.UnitPrice;
            var nameShort = name.Length > 28 ? name.Substring(0, 28) + "…" : name;
            var safeNameShort = HtmlEscape(nameShort);
            lines.Add($"{safeNameShort,-30} {it.Qty,5:N0} x {it.UnitPrice,9:N0} = {sum,10:N0}");
        }

        var itemsCount = sale.Items?.Count ?? 0;
        var qty = sale.Items?.Sum(i => i.Qty) ?? 0m;
        var paymentRu = PaymentTypeRu(sale.PaymentType);
        var safeClient = HtmlEscape(clientName);
        var safeManager = HtmlEscape(managerDisplay);

        var title = $"<b>Продажа #{sale.Id}</b>";
        var header = $"Дата: {localTime:yyyy-MM-dd HH:mm}\nКлиент: {safeClient}\nОплата: {paymentRu}\nПозиции: {itemsCount} (шт: {qty:N0})\nИтого: {sale.Total:N0}\nМенеджер: {safeManager}";
        var itemsBlock = lines.Count > 0 ? ("\n<pre>" + string.Join("\n", lines) + "</pre>") : string.Empty;
        return title + "\n" + header + itemsBlock;
    }

    [HttpPost("/api/sales/{saleId:int}/photo")]
    [Authorize(Policy = "ManagerOnly")]
    [RequestSizeLimit(20_000_000)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadSalePhoto([FromRoute] int saleId, CancellationToken ct)
    {
        var sale = await _sales.GetByIdAsync(saleId, ct);
        if (sale is null) return ValidationProblem(detail: $"Sale not found: {saleId}");

        if (!Request.HasFormContentType) return ValidationProblem(detail: "Expected multipart/form-data");
        var form = await Request.ReadFormAsync(ct);
        var file = form.Files["file"] ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0) return ValidationProblem(detail: "Photo file is required");

        var caption = await BuildSaleCaptionAsync(sale, ct);
        var ids = _tgSettings.ParseAllowedChatIds();
        if (ids.Count == 0) return NoContent();

        await using var stream = file.OpenReadStream();
        foreach (var chatId in ids)
        {
            stream.Position = 0;
            var ok = await _tg.SendPhotoAsync(chatId, stream, file.FileName ?? $"sale_{saleId}.jpg", caption, "HTML", ct);
            if (!ok) _logger.LogWarning("Failed to send sale photo to chat {ChatId} for sale {SaleId}", chatId, saleId);
        }

        // Store last photo per manager until next sale
        try
        {
            var user = sale.CreatedBy ?? "unknown";
            var baseDir = Path.Combine(AppContext.BaseDirectory, "sale-photos");
            Directory.CreateDirectory(baseDir);
            var savePath = Path.Combine(baseDir, $"{user}_{saleId}_{DateTime.UtcNow:yyyyMMddHHmmss}.jpg");
            stream.Position = 0;
            await using (var fs = System.IO.File.Create(savePath))
            {
                await stream.CopyToAsync(fs, ct);
            }
            // Delete previous photos for this user
            var prev = await _db.SalePhotos.Where(p => p.UserName == user && p.SaleId != saleId).ToListAsync(ct);
            foreach (var p in prev)
            {
                try { if (!string.IsNullOrWhiteSpace(p.PathOrBlob) && System.IO.File.Exists(p.PathOrBlob)) System.IO.File.Delete(p.PathOrBlob); } catch { }
            }
            _db.SalePhotos.RemoveRange(prev);
            // Upsert current record (remove any duplicates for same sale)
            var currPrev = await _db.SalePhotos.Where(p => p.SaleId == saleId).ToListAsync(ct);
            _db.SalePhotos.RemoveRange(currPrev);
            _db.SalePhotos.Add(new SalePhoto { SaleId = sale.Id, UserName = user, Mime = file.ContentType, Size = file.Length, CreatedAt = DateTime.UtcNow, PathOrBlob = savePath });
            await _db.SaveChangesAsync(ct);
        }
        catch { }

        return NoContent();
    }
}

