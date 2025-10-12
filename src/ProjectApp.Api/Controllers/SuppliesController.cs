using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SuppliesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<SuppliesController> _logger;

    public SuppliesController(AppDbContext db, ILogger<SuppliesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IEnumerable<Batch>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] SupplyCreateDto dto, CancellationToken ct)
    {
        if (dto.Items is null || dto.Items.Count == 0)
            return ValidationProblem(detail: "At least one supply item is required");

        var created = new List<Batch>();

        foreach (var it in dto.Items)
        {
            if (it.ProductId <= 0 || it.Qty <= 0)
                return ValidationProblem(detail: $"Invalid item: productId={it.ProductId}, qty={it.Qty}");

            // Ensure product exists
            var exists = await _db.Products.AnyAsync(p => p.Id == it.ProductId, ct);
            if (!exists) return ValidationProblem(detail: $"Product not found: {it.ProductId}");

            // Increase ND40 stock
            var stockNd = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == it.ProductId && s.Register == StockRegister.ND40, ct);
            if (stockNd is null)
            {
                stockNd = new Stock { ProductId = it.ProductId, Register = StockRegister.ND40, Qty = 0m };
                _db.Stocks.Add(stockNd);
            }
            stockNd.Qty += it.Qty;

            // Add batch in ND40
            var b = new Batch
            {
                ProductId = it.ProductId,
                Register = StockRegister.ND40,
                Qty = it.Qty,
                UnitCost = it.UnitCost,
                CreatedAt = DateTime.UtcNow,
                Code = string.IsNullOrWhiteSpace(it.Code) ? null : it.Code.Trim(),
                Note = it.Note
            };
            _db.Batches.Add(b);
            created.Add(b);

            // Inventory transaction (purchase)
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                ProductId = it.ProductId,
                Register = StockRegister.ND40,
                Type = InventoryTransactionType.Purchase,
                Qty = it.Qty,
                UnitCost = it.UnitCost,
                BatchId = null, // will not have ID until save; acceptable for audit
                CreatedAt = DateTime.UtcNow,
                Note = string.IsNullOrWhiteSpace(it.Code) ? "supply" : $"supply code={it.Code}"
            });
        }

        await _db.SaveChangesAsync(ct);
        return Created("/api/supplies", created);
    }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IEnumerable<Batch>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Query([FromQuery] string? code, [FromQuery] int? productId, [FromQuery] string? register, CancellationToken ct)
    {
        var q = _db.Batches.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(code)) q = q.Where(b => b.Code == code);
        if (productId.HasValue) q = q.Where(b => b.ProductId == productId.Value);
        if (!string.IsNullOrWhiteSpace(register) && Enum.TryParse<StockRegister>(register, out var reg)) q = q.Where(b => b.Register == reg);
        var list = await q.OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id).ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost("{code}/to-im40")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MoveToIm40([FromRoute] string code, [FromBody] SupplyTransferDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return ValidationProblem(detail: "Code is required");
        if (dto.Items is null || dto.Items.Count == 0) return ValidationProblem(detail: "At least one transfer item is required");

        foreach (var it in dto.Items)
        {
            if (it.ProductId <= 0 || it.Qty <= 0)
                return ValidationProblem(detail: $"Invalid transfer item: productId={it.ProductId}, qty={it.Qty}");

            var remain = it.Qty;
            var ndBatches = await _db.Batches
                .Where(b => b.ProductId == it.ProductId && b.Register == StockRegister.ND40 && b.Code == code && b.Qty > 0)
                .OrderBy(b => b.CreatedAt).ThenBy(b => b.Id)
                .ToListAsync(ct);

            decimal moved = 0m;
            foreach (var b in ndBatches)
            {
                if (remain <= 0) break;
                var take = Math.Min(b.Qty, remain);
                if (take <= 0) continue;

                // decrease ND40 batch
                b.Qty -= take;
                moved += take;

                // Log ND40 move out
                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductId = it.ProductId,
                    Register = StockRegister.ND40,
                    Type = InventoryTransactionType.MoveNdToIm,
                    Qty = -take,
                    UnitCost = b.UnitCost,
                    BatchId = b.Id,
                    CreatedAt = DateTime.UtcNow,
                    Note = $"move ND->IM code={code}"
                });

                // increase IM40 stock and add IM40 batch with same cost and code
                var stockIm = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == it.ProductId && s.Register == StockRegister.IM40, ct);
                if (stockIm is null)
                {
                    stockIm = new Stock { ProductId = it.ProductId, Register = StockRegister.IM40, Qty = 0m };
                    _db.Stocks.Add(stockIm);
                }
                stockIm.Qty += take;

                var imBatch = new Batch
                {
                    ProductId = it.ProductId,
                    Register = StockRegister.IM40,
                    Qty = take,
                    UnitCost = b.UnitCost,
                    CreatedAt = DateTime.UtcNow,
                    Code = b.Code,
                    Note = $"move from ND40 code={code}"
                };
                _db.Batches.Add(imBatch);

                // Log IM40 move in (batchId unknown until save)
                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductId = it.ProductId,
                    Register = StockRegister.IM40,
                    Type = InventoryTransactionType.MoveNdToIm,
                    Qty = take,
                    UnitCost = b.UnitCost,
                    BatchId = null,
                    CreatedAt = DateTime.UtcNow,
                    Note = $"move ND->IM code={code}"
                });
            }

            // decrease ND40 stock for moved qty
            if (moved > 0)
            {
                var stockNd = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == it.ProductId && s.Register == StockRegister.ND40, ct);
                if (stockNd is null) stockNd = new Stock { ProductId = it.ProductId, Register = StockRegister.ND40, Qty = 0m };
                stockNd.Qty -= moved;
            }

            if (moved < it.Qty)
            {
                return ValidationProblem(detail: $"Insufficient ND40 quantity for product {it.ProductId} with code {code}. Requested={it.Qty}, Moved={moved}");
            }
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { message = $"Moved to IM40 for code={code}" });
    }
}
