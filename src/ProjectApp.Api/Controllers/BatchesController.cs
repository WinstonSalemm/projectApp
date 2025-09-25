using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BatchesController(AppDbContext db) : ControllerBase
{
    // GET /api/batches?productId=&register=&from=&to=&page=&size=
    [HttpGet]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> List([FromQuery] int? productId, [FromQuery] StockRegister? register,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page = 1, [FromQuery] int size = 50)
    {
        var q = db.Batches.AsNoTracking().AsQueryable();
        if (productId.HasValue) q = q.Where(b => b.ProductId == productId.Value);
        if (register.HasValue) q = q.Where(b => b.Register == register.Value);
        if (from.HasValue) q = q.Where(b => b.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(b => b.CreatedAt < to.Value);
        page = Math.Max(1, page); size = Math.Clamp(size, 1, 200);
        var total = await q.CountAsync();
        var items = await q.OrderBy(b => b.CreatedAt).ThenBy(b => b.Id).Skip((page-1)*size).Take(size).ToListAsync();
        return Ok(new { items, total, page, size });
    }

    // POST /api/batches  (приёмка/корректировка)
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(Batch), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] BatchCreateDto dto, CancellationToken ct)
    {
        if (dto.Qty == 0) return ValidationProblem("Qty must be non-zero");
        var b = new Batch
        {
            ProductId = dto.ProductId,
            Register = dto.Register,
            Qty = dto.Qty,
            UnitCost = dto.UnitCost,
            CreatedAt = DateTime.UtcNow,
            Note = dto.Note
        };
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Batches.Add(b);
        var stock = await db.Stocks.FirstOrDefaultAsync(s => s.ProductId == b.ProductId && s.Register == b.Register, ct);
        if (stock is null)
        {
            stock = new Stock { ProductId = b.ProductId, Register = b.Register, Qty = 0m };
            db.Stocks.Add(stock);
        }
        stock.Qty += b.Qty; // если Qty отрицательное — произойдет списание с проверки на минус в домене продаж
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Created($"/api/batches/{b.Id}", b);
    }

    // PUT /api/batches/{id}  (корректировка себестоимости/даты/ноты)
    [HttpPut("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(Batch), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] BatchUpdateDto dto)
    {
        var b = await db.Batches.FindAsync(id);
        if (b is null) return NotFound();
        if (dto.UnitCost.HasValue) b.UnitCost = dto.UnitCost.Value;
        if (!string.IsNullOrWhiteSpace(dto.Note)) b.Note = dto.Note;
        if (dto.CreatedAt.HasValue) b.CreatedAt = dto.CreatedAt.Value;
        await db.SaveChangesAsync();
        return Ok(b);
    }
}
