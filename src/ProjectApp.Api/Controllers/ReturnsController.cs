using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Models;
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

    public ReturnsController(AppDbContext db, ILogger<ReturnsController> logger, ProjectApp.Api.Integrations.Telegram.IReturnsNotifier retNotifier)
    {
        _db = db;
        _logger = logger;
        _retNotifier = retNotifier;
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
        return Ok(ret);
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

                _logger.LogInformation("Return created {ReturnId} for sale {SaleId} client {ClientId} sum {Sum} payment {PaymentType}",
                    retFull.Id, sale.Id, retFull.ClientId, retFull.Sum, sale.PaymentType);
                // notify
                await _retNotifier.NotifyReturnAsync(retFull, sale, ct);
                return CreatedAtAction(nameof(GetById), new { id = retFull.Id }, retFull);
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
            await RestockPartialReturnByBatchesAsync(sale, retPartial.Items, ct);
            await _db.SaveChangesAsync(ct);
            // notify
            await _retNotifier.NotifyReturnAsync(retPartial, sale, ct);
            _logger.LogInformation("Partial return {ReturnId} for sale {SaleId} client {ClientId} sum {Sum}", retPartial.Id, sale.Id, retPartial.ClientId, retPartial.Sum);
            return CreatedAtAction(nameof(GetById), new { id = retPartial.Id }, retPartial);
        }
        catch (Exception ex)
        {
            return ValidationProblem(detail: ex.Message);
        }
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
            }
        }
    }

    // Restock for partial return: distribute across original batches FIFO, fallback if no consumption
    private async Task RestockPartialReturnByBatchesAsync(Sale sale, List<ReturnItem> items, CancellationToken ct)
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

        return Ok(list);
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

        return Ok(list);
    }
}

