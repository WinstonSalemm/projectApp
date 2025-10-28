using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectApp.Api.Data;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Models;
using ProjectApp.Api.Integrations.Telegram;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System;
 using System.Linq;
 using System.Collections.Generic;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReturnsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<ReturnsController> _logger;
    private readonly ProjectApp.Api.Integrations.Telegram.IReturnsNotifier _retNotifier;
    private readonly ITelegramService _tg;
    private readonly TelegramSettings _tgSettings;

    public ReturnsController(AppDbContext db, ILogger<ReturnsController> logger, ProjectApp.Api.Integrations.Telegram.IReturnsNotifier retNotifier, ITelegramService tg, IOptions<TelegramSettings> tgOptions)
    {
        _db = db;
        _logger = logger;
        _retNotifier = retNotifier;
        _tg = tg;
        _tgSettings = tgOptions.Value;
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Return), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken ct)
    {
        var ret = await _db.Returns
            .AsNoTracking()
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (ret is null) return NotFound();
        
        // Return DTO to avoid circular reference
        var result = new
        {
            ret.Id,
            ret.RefSaleId,
            ret.ClientId,
            ret.Sum,
            ret.Reason,
            ret.CreatedAt,
            Items = ret.Items.Select(i => new
            {
                i.Id,
                i.SaleItemId,
                i.Qty,
                i.UnitPrice
            }).ToList()
        };
        return Ok(result);
    }

    [HttpGet("history")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(IEnumerable<Return>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory([FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo, CancellationToken ct)
    {
        var query = _db.Returns.AsNoTracking().AsQueryable();
        
        if (dateFrom.HasValue)
            query = query.Where(r => r.CreatedAt >= dateFrom.Value);
        
        if (dateTo.HasValue)
            query = query.Where(r => r.CreatedAt < dateTo.Value);
        
        var returns = await query.OrderByDescending(r => r.Id).ToListAsync(ct);
        
        // Return DTOs to avoid circular reference
        var result = returns.Select(r => new
        {
            r.Id,
            r.RefSaleId,
            r.ClientId,
            r.Sum,
            r.Reason,
            r.CreatedAt
        }).ToList();
        
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(Return), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] ReturnCreateDto dto, CancellationToken ct)
    {
        try
        {
            if (dto.RefSaleId <= 0)
                return ValidationProblem(detail: "RefSaleId is required for return");

            var sale = await _db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == dto.RefSaleId, ct);
            if (sale is null)
                return ValidationProblem(detail: $"Sale not found: {dto.RefSaleId}");

            // Business rule: only a single return per sale is allowed
            var alreadyExists = await _db.Returns.AnyAsync(r => r.RefSaleId == sale.Id, ct);
            if (alreadyExists)
                return ValidationProblem(detail: $"Return for sale #{sale.Id} already exists. You can cancel it if created by mistake.");

            // Full return (restock by original batches)
            if (dto.Items is null || dto.Items.Count == 0)
            {
                // Build return with items matching sale items to have ReturnItemIds for audit
                var retFull = new Return
                {
                    RefSaleId = sale.Id,
                    ClientId = dto.ClientId ?? sale.ClientId,
                    Sum = sale.Total,
                    CreatedAt = DateTime.UtcNow,
                    Reason = dto.Reason,
                    Items = sale.Items.Select(si => new ReturnItem
                    {
                        SaleItemId = si.Id,
                        Qty = si.Qty,
                        UnitPrice = si.UnitPrice
                    }).ToList()
                };
                _db.Returns.Add(retFull);
                await _db.SaveChangesAsync(ct); // need ReturnItemIds

                await RestockFullReturnByBatchesAsync(sale, retFull, ct);
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("Return created {ReturnId} for sale {SaleId} client {ClientId} sum {Sum} payment {PaymentType}", retFull.Id, sale.Id, retFull.ClientId, retFull.Sum, sale.PaymentType);
                // notify (defer if waiting for photo from client)
                if (!(dto.WaitForPhoto ?? false))
                {
                    await _retNotifier.NotifyReturnAsync(retFull, sale, ct);
                }
                // Return DTO to avoid circular reference
                var resultFull = new
                {
                    retFull.Id,
                    retFull.RefSaleId,
                    retFull.ClientId,
                    retFull.Sum,
                    retFull.Reason,
                    retFull.CreatedAt
                };
                return CreatedAtAction(nameof(GetById), new { id = retFull.Id }, resultFull);
            }

            // Partial return
            var saleItemIds = dto.Items.Select(i => i.SaleItemId).ToHashSet();
            var saleItemsMap = sale.Items.Where(si => saleItemIds.Contains(si.Id))
                                         .ToDictionary(si => si.Id);

            if (saleItemsMap.Count != saleItemIds.Count)
            {
                var badIds = string.Join(",", saleItemIds.Except(saleItemsMap.Keys));
                return ValidationProblem(detail: $"Some sale items do not belong to sale {sale.Id}: [{badIds}]");
            }

            var alreadyReturned = await _db.ReturnItems
                .Where(ri => saleItemIds.Contains(ri.SaleItemId))
                .GroupBy(ri => ri.SaleItemId)
                .Select(g => new { SaleItemId = g.Key, Qty = g.Sum(x => x.Qty) })
                .ToDictionaryAsync(x => x.SaleItemId, x => x.Qty, ct);

            var returnItems = new List<ReturnItem>();
            decimal total = 0m;

            foreach (var it in dto.Items)
            {
                if (it.Qty <= 0)
                    return ValidationProblem(detail: $"Qty must be > 0 for saleItemId={it.SaleItemId}");

                var si = saleItemsMap[it.SaleItemId];
                var doneQty = alreadyReturned.TryGetValue(si.Id, out var q) ? q : 0m;
                var maxAvail = si.Qty - doneQty;

                if (it.Qty > maxAvail)
                    return ValidationProblem(detail: $"Return qty {it.Qty} exceeds available {maxAvail} for saleItemId={si.Id}");

                var lineSum = it.Qty * si.UnitPrice;
                total += lineSum;

                returnItems.Add(new ReturnItem
                {
                    Qty = it.Qty,
                    UnitPrice = si.UnitPrice,
                    SaleItemId = si.Id
                });
            }

            var retPartial = new Return
            {
                RefSaleId = sale.Id,
                ClientId = dto.ClientId ?? sale.ClientId,
                Sum = total,
                CreatedAt = DateTime.UtcNow,
                Reason = dto.Reason,
                Items = returnItems
            };

            _db.Returns.Add(retPartial);
            await _db.SaveChangesAsync(ct); // ensure ReturnItemIds
            await RestockPartialReturnByBatchesAsync(sale, retPartial, retPartial.Items, ct);
            await _db.SaveChangesAsync(ct);
            // notify (defer if waiting for photo from client)
            if (!(dto.WaitForPhoto ?? false))
            {
                await _retNotifier.NotifyReturnAsync(retPartial, sale, ct);
            }
            _logger.LogInformation("Partial return {ReturnId} for sale {SaleId} client {ClientId} sum {Sum}", retPartial.Id, sale.Id, retPartial.ClientId, retPartial.Sum);
            // Return DTO to avoid circular reference
            var resultPartial = new
            {
                retPartial.Id,
                retPartial.RefSaleId,
                retPartial.ClientId,
                retPartial.Sum,
                retPartial.Reason,
                retPartial.CreatedAt
            };
            return CreatedAtAction(nameof(GetById), new { id = retPartial.Id }, resultPartial);
        }
        catch (Exception ex)
        {
            return ValidationProblem(detail: ex.Message);
        }
    }

    [HttpPost("{id:int}/photo")]
    [Authorize(Policy = "ManagerOnly")]
    [RequestSizeLimit(20_000_000)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadPhoto([FromRoute] int id, CancellationToken ct)
    {
        if (!Request.HasFormContentType) return ValidationProblem(detail: "Expected multipart/form-data");
        var form = await Request.ReadFormAsync(ct);
        var file = form.Files["file"] ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0) return ValidationProblem(detail: "Photo file is required");

        var ret = await _db.Returns.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (ret is null) return ValidationProblem(detail: $"Return not found: {id}");
        var sale = await _db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == ret.RefSaleId, ct);
        if (sale is null) return ValidationProblem(detail: $"Sale not found for return {id}");

        // Recompress to JPEG
        await using var inStream = file.OpenReadStream();
        using var image = await Image.LoadAsync(inStream, ct);
        var longSide = Math.Max(image.Width, image.Height);
        if (longSide > 1600) // reuse a sane limit
        {
            var scale = 1600.0 / longSide;
            var newW = (int)Math.Round(image.Width * scale);
            var newH = (int)Math.Round(image.Height * scale);
            image.Mutate(x => x.Resize(newW, newH));
        }
        using var outMs = new MemoryStream();
        var enc = new JpegEncoder { Quality = 85 };
        await image.SaveAsJpegAsync(outMs, enc, ct);
        outMs.Position = 0;

        var caption = await BuildReturnCaptionHtmlAsync(ret, sale, ct);
        var ids = _tgSettings.ParseAllowedChatIds();
        foreach (var chatId in ids)
        {
            outMs.Position = 0;
            var ok = await _tg.SendPhotoAsync(chatId, outMs, file.FileName ?? $"return_{id}.jpg", caption, "HTML", ct);
            if (!ok) _logger.LogWarning("Failed to send return photo to chat {ChatId} for return {Id}", chatId, id);
        }

        return NoContent();
    }

    private async Task<string> BuildReturnCaptionHtmlAsync(Return ret, Sale sale, CancellationToken ct)
    {
        string Html(string? s) => string.IsNullOrEmpty(s) ? string.Empty : s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        var localTime = ret.CreatedAt.AddMinutes(_tgSettings.TimeZoneOffsetMinutes);
        var clientName = string.IsNullOrWhiteSpace(sale.ClientName) ? "Посетитель" : sale.ClientName;
        var safeClient = Html(clientName);

        var pids = sale.Items?.Select(i => i.ProductId).Distinct().ToList() ?? new List<int>();
        var prodMap = await _db.Products.AsNoTracking()
            .Where(p => pids.Contains(p.Id))
            .Select(p => new { p.Id, p.Sku, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p, ct);

        var lines = new List<string>();
        int itemsCount = ret.Items?.Count ?? 0;
        decimal totalQty = 0m;
        decimal totalSum = 0m;
        foreach (var ri in ret.Items ?? new List<ReturnItem>())
        {
            var si = sale.Items.FirstOrDefault(x => x.Id == ri.SaleItemId);
            var pid = si?.ProductId ?? 0;
            prodMap.TryGetValue(pid, out var p);
            var name = p?.Name ?? $"#{pid}";
            var sum = ri.Qty * ri.UnitPrice;
            totalQty += ri.Qty;
            totalSum += sum;
            var nameShort = name.Length > 28 ? name.Substring(0, 28) + "…" : name;
            var safeNameShort = Html(nameShort);
            lines.Add($"{safeNameShort,-30} {ri.Qty,5:N0} x {ri.UnitPrice,9:N0} = {sum,10:N0}");
        }

        var title = $"<b>Возврат #{ret.Id}</b>";
        var header = $"По продаже: #{sale.Id}\nДата: {localTime:yyyy-MM-dd HH:mm}\nКлиент: {safeClient}\nПозиции: {itemsCount} (шт: {totalQty:N0})\nИтого: {totalSum:N0} сум";
        var itemsBlock = lines.Count > 0 ? ("\n<pre>" + string.Join("\n", lines) + "</pre>") : string.Empty;
        return title + "\n" + header + itemsBlock;
    }

    [HttpPost("/api/sales/{saleId:int}/return/cancel")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelBySale([FromRoute] int saleId, CancellationToken ct)
    {
        var ret = await _db.Returns.Include(r => r.Items).Where(r => r.RefSaleId == saleId)
            .OrderByDescending(r => r.Id).FirstOrDefaultAsync(ct);
        if (ret is null) return NotFound();

        var sale = await _db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == saleId, ct);
        if (sale is null) return NotFound();

        await ReverseReturnRestockAsync(sale, ret, ct);
        // Delete return and its items + restock records
        var restocks = await _db.ReturnItemRestocks.Where(x => x.ReturnItemId == 0 || ret.Items.Select(i => i.Id).Contains(x.ReturnItemId)).ToListAsync(ct);
        _db.ReturnItemRestocks.RemoveRange(restocks);
        _db.ReturnItems.RemoveRange(ret.Items);
        _db.Returns.Remove(ret);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static StockRegister MapPaymentToRegister(PaymentType payment)
    {
        return payment switch
        {
            PaymentType.CashWithReceipt or PaymentType.CardWithReceipt or PaymentType.ClickWithReceipt or PaymentType.Site or PaymentType.Return => StockRegister.IM40,
            PaymentType.CashNoReceipt or PaymentType.ClickNoReceipt or PaymentType.Click or PaymentType.Payme => StockRegister.ND40,
            PaymentType.Reservation => StockRegister.IM40,
            _ => StockRegister.IM40
        };
    }

    // Adjust stock in specified register by deltaQty
    private async Task AdjustStockAsync(int productId, StockRegister register, decimal deltaQty, CancellationToken ct)
    {
        var stock = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == productId && s.Register == register, ct);
        if (stock is null)
        {
            stock = new Stock { ProductId = productId, Register = register, Qty = 0m };
            _db.Stocks.Add(stock);
        }
        stock.Qty += deltaQty;
    }

    // Restock for full return: use recorded consumption; fallback to payment register if consumption missing
    private async Task RestockFullReturnByBatchesAsync(Sale sale, Return ret, CancellationToken ct)
    {
        foreach (var ri in ret.Items)
        {
            var si = sale.Items.First(s => s.Id == ri.SaleItemId);
            var consList = await _db.SaleItemConsumptions
                .Where(c => c.SaleItemId == si.Id)
                .OrderBy(c => c.Id)
                .ToListAsync(ct);

            if (consList.Count == 0)
            {
                // Fallback: as before
                var register = MapPaymentToRegister(sale.PaymentType);
                await AdjustStockAsync(si.ProductId, register, +ri.Qty, ct);
                var newBatch = new Batch
                {
                    ProductId = si.ProductId,
                    Register = register,
                    Qty = ri.Qty,
                    UnitCost = 0m,
                    CreatedAt = DateTime.UtcNow,
                    Note = $"return full sale #{sale.Id}"
                };
                _db.Batches.Add(newBatch);
                await _db.SaveChangesAsync(ct);
                _db.ReturnItemRestocks.Add(new ReturnItemRestock { ReturnItemId = ri.Id, SaleItemId = si.Id, BatchId = newBatch.Id, Qty = ri.Qty });
                continue;
            }

            foreach (var c in consList)
            {
                // how much already restocked for this saleItem+batch
                var already = await _db.ReturnItemRestocks
                    .Where(r => r.SaleItemId == si.Id && r.BatchId == c.BatchId)
                    .SumAsync(r => (decimal?)r.Qty, ct) ?? 0m;
                var avail = c.Qty - already;
                if (avail <= 0) continue;
                var batch = await _db.Batches.FirstOrDefaultAsync(b => b.Id == c.BatchId, ct);
                if (batch is null) continue; // batch deleted? skip

                // restore entire avail for full return
                batch.Qty += avail;
                await AdjustStockAsync(si.ProductId, batch.Register, +avail, ct);
                _db.ReturnItemRestocks.Add(new ReturnItemRestock { ReturnItemId = ri.Id, SaleItemId = si.Id, BatchId = batch.Id, Qty = avail });
                // Log inventory transaction (ReturnIn)
                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductId = si.ProductId,
                    Register = batch.Register,
                    Type = InventoryTransactionType.ReturnIn,
                    Qty = avail,
                    UnitCost = batch.UnitCost,
                    BatchId = batch.Id,
                    ReturnId = ret.Id,
                    CreatedAt = DateTime.UtcNow,
                    Note = $"return full sale #{sale.Id}"
                });
            }
        }
    }

    // Restock for partial return: distribute across original batches FIFO, fallback if no consumption
    private async Task RestockPartialReturnByBatchesAsync(Sale sale, Return ret, List<ReturnItem> items, CancellationToken ct)
    {
        foreach (var ri in items)
        {
            var si = sale.Items.First(s => s.Id == ri.SaleItemId);
            var toReturn = ri.Qty;
            var consList = await _db.SaleItemConsumptions
                .Where(c => c.SaleItemId == si.Id)
                .OrderBy(c => c.Id)
                .ToListAsync(ct);

            foreach (var c in consList)
            {
                if (toReturn <= 0) break;
                var already = await _db.ReturnItemRestocks
                    .Where(r => r.SaleItemId == si.Id && r.BatchId == c.BatchId)
                    .SumAsync(r => (decimal?)r.Qty, ct) ?? 0m;
                var avail = c.Qty - already;
                if (avail <= 0) continue;
                var take = Math.Min(avail, toReturn);
                var batch = await _db.Batches.FirstOrDefaultAsync(b => b.Id == c.BatchId, ct);
                if (batch is null) continue;
                batch.Qty += take;
                await AdjustStockAsync(si.ProductId, batch.Register, +take, ct);
                _db.ReturnItemRestocks.Add(new ReturnItemRestock { ReturnItemId = ri.Id, SaleItemId = si.Id, BatchId = batch.Id, Qty = take });
                // Log inventory transaction (ReturnIn)
                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductId = si.ProductId,
                    Register = batch.Register,
                    Type = InventoryTransactionType.ReturnIn,
                    Qty = take,
                    UnitCost = batch.UnitCost,
                    BatchId = batch.Id,
                    ReturnId = ret.Id,
                    CreatedAt = DateTime.UtcNow,
                    Note = $"partial return saleItem #{si.Id}"
                });
                toReturn -= take;
            }

            if (toReturn > 0)
            {
                // Fallback for remaining: payment register
                var register = MapPaymentToRegister(sale.PaymentType);
                await AdjustStockAsync(si.ProductId, register, +toReturn, ct);
                var newBatch = new Batch
                {
                    ProductId = si.ProductId,
                    Register = register,
                    Qty = toReturn,
                    UnitCost = 0m,
                    CreatedAt = DateTime.UtcNow,
                    Note = $"partial return saleItem #{si.Id}"
                };
                _db.Batches.Add(newBatch);
                await _db.SaveChangesAsync(ct);
                _db.ReturnItemRestocks.Add(new ReturnItemRestock { ReturnItemId = ri.Id, SaleItemId = si.Id, BatchId = newBatch.Id, Qty = toReturn });
                // Log inventory transaction (ReturnIn, fallback batch)
                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductId = si.ProductId,
                    Register = register,
                    Type = InventoryTransactionType.ReturnIn,
                    Qty = toReturn,
                    UnitCost = newBatch.UnitCost,
                    BatchId = newBatch.Id,
                    ReturnId = ret.Id,
                    CreatedAt = DateTime.UtcNow,
                    Note = $"partial return fallback saleItem #{si.Id}"
                });
            }
        }
    }

    // Reverse previously recorded restocks for a return (used when cancelling a return)
    private async Task ReverseReturnRestockAsync(Sale sale, Return ret, CancellationToken ct)
    {
        var retItemIds = ret.Items.Select(i => i.Id).ToHashSet();
        var restocks = await _db.ReturnItemRestocks
            .Where(r => retItemIds.Contains(r.ReturnItemId))
            .OrderBy(r => r.Id)
            .ToListAsync(ct);

        var saleItems = sale.Items.ToDictionary(si => si.Id, si => si);

        foreach (var rs in restocks)
        {
            if (!saleItems.TryGetValue(rs.SaleItemId, out var si))
                continue;

            var batch = await _db.Batches.FirstOrDefaultAsync(b => b.Id == rs.BatchId, ct);
            if (batch is null)
                continue;

            // Decrement batch quantity and stock by the restocked amount
            batch.Qty -= rs.Qty;
            await AdjustStockAsync(si.ProductId, batch.Register, -rs.Qty, ct);
            // Log inventory transaction (ReturnOut - reverse)
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                ProductId = si.ProductId,
                Register = batch.Register,
                Type = InventoryTransactionType.ReturnOut,
                Qty = -rs.Qty,
                UnitCost = batch.UnitCost,
                BatchId = batch.Id,
                ReturnId = ret.Id,
                CreatedAt = DateTime.UtcNow,
                Note = $"reverse restock for return #{ret.Id}"
            });
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Return>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Query([FromQuery] int? refSaleId, [FromQuery] int? clientId, [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo, CancellationToken ct)
    {
        var q = _db.Returns.AsQueryable();

        if (refSaleId.HasValue) q = q.Where(r => r.RefSaleId == refSaleId.Value);
        if (clientId.HasValue) q = q.Where(r => r.ClientId == clientId.Value);
        if (dateFrom.HasValue) q = q.Where(r => r.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue) q = q.Where(r => r.CreatedAt < dateTo.Value);

        var list = await q
            .OrderByDescending(r => r.Id)
            .Include(r => r.Items)
            .ToListAsync(ct);

        // Return DTOs to avoid circular reference
        var result = list.Select(r => new
        {
            r.Id,
            r.RefSaleId,
            r.ClientId,
            r.Sum,
            r.Reason,
            r.CreatedAt,
            Items = r.Items.Select(i => new
            {
                i.Id,
                i.SaleItemId,
                i.Qty,
                i.UnitPrice
            }).ToList()
        }).ToList();

        return Ok(result);
    }

    [HttpGet("/api/sales/{saleId:int}/returns")]
    [ProducesResponseType(typeof(IEnumerable<Return>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBySale([FromRoute] int saleId, CancellationToken ct)
    {
        var list = await _db.Returns
            .Where(r => r.RefSaleId == saleId)
            .OrderBy(r => r.Id)
            .Include(r => r.Items)
            .ToListAsync(ct);

        // Return DTOs to avoid circular reference
        var result = list.Select(r => new
        {
            r.Id,
            r.RefSaleId,
            r.ClientId,
            r.Sum,
            r.Reason,
            r.CreatedAt,
            Items = r.Items.Select(i => new
            {
                i.Id,
                i.SaleItemId,
                i.Qty,
                i.UnitPrice
            }).ToList()
        }).ToList();

        return Ok(result);
    }
}

