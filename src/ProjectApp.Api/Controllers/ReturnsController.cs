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

            // Full return (backward compatible behavior)
            if (dto.Items is null || dto.Items.Count == 0)
            {
                var register = MapPaymentToRegister(sale.PaymentType);

                // Increase stock back for full sale quantities
                foreach (var it in sale.Items)
                {
                    var stock = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == it.ProductId && s.Register == register, ct);
                    if (stock is null)
                    {
                        stock = new Stock { ProductId = it.ProductId, Register = register, Qty = 0m };
                        _db.Stocks.Add(stock);
                    }
                    stock.Qty += it.Qty;

                    // Add batch entry for returned goods (UnitCost=0 by default)
                    _db.Batches.Add(new Batch
                    {
                        ProductId = it.ProductId,
                        Register = register,
                        Qty = it.Qty,
                        UnitCost = 0m,
                        CreatedAt = DateTime.UtcNow,
                        Note = $"return full sale #{sale.Id}"
                    });
                }

                var retFull = new Return
                {
                    RefSaleId = sale.Id,
                    ClientId = dto.ClientId ?? sale.ClientId,
                    Sum = sale.Total,
                    CreatedAt = DateTime.UtcNow,
                    Reason = dto.Reason
                };

                _db.Returns.Add(retFull);
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("Return created {ReturnId} for sale {SaleId} client {ClientId} sum {Sum} payment {PaymentType} register {Register}",
                    retFull.Id, sale.Id, retFull.ClientId, retFull.Sum, sale.PaymentType, register);
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
            foreach (var ri in returnItems)
            {
                var si = saleItemsMap[ri.SaleItemId];
                await AdjustStockAsync(si.ProductId, StockRegister.ND40, +ri.Qty, ct);

                // Also add batch for returned qty in ND40
                _db.Batches.Add(new Batch
                {
                    ProductId = si.ProductId,
                    Register = StockRegister.ND40,
                    Qty = ri.Qty,
                    UnitCost = 0m,
                    CreatedAt = DateTime.UtcNow,
                    Note = $"partial return saleItem #{si.Id}"
                });
            }

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

