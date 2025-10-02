using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StocksController : ControllerBase
{
    private readonly AppDbContext _db;
    public StocksController(AppDbContext db) { _db = db; }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IEnumerable<StockViewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] string? query, [FromQuery] string? category, CancellationToken ct)
    {
        var products = _db.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(category))
        {
            var c = category.Trim();
            products = products.Where(p => p.Category == c);
        }
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            products = products.Where(p => EF.Functions.Like(p.Sku, $"%{q}%") || EF.Functions.Like(p.Name, $"%{q}%"));
        }

        var stocks = await (
            from p in products
            join s in _db.Stocks.AsNoTracking() on p.Id equals s.ProductId into ps
            from s in ps.DefaultIfEmpty()
            group s by new { p.Id, p.Sku, p.Name, p.Category } into g
            select new StockViewDto
            {
                ProductId = g.Key.Id,
                Sku = g.Key.Sku,
                Name = g.Key.Name,
                Category = g.Key.Category ?? string.Empty,
                Nd40Qty = g.Sum(x => x != null && x.Register == StockRegister.ND40 ? x.Qty : 0m),
                Im40Qty = g.Sum(x => x != null && x.Register == StockRegister.IM40 ? x.Qty : 0m),
                TotalQty = g.Sum(x => x != null ? x.Qty : 0m)
            }
        ).OrderBy(r => r.ProductId).ToListAsync(ct);

        return Ok(stocks);
    }

    // GET /api/stocks/batches?query=&category=
    [HttpGet("batches")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IEnumerable<BatchStockViewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByBatches([FromQuery] string? query, [FromQuery] string? category, CancellationToken ct)
    {
        var products = _db.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(category))
        {
            var c = category.Trim();
            products = products.Where(p => p.Category == c);
        }
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            products = products.Where(p => EF.Functions.Like(p.Sku, $"%{q}%") || EF.Functions.Like(p.Name, $"%{q}%"));
        }

        var prodList = await products
            .Select(p => new { p.Id, p.Sku, p.Name, p.Category })
            .OrderBy(p => p.Id)
            .ToListAsync(ct);

        if (prodList.Count == 0)
        {
            return Ok(new List<BatchStockViewDto>());
        }

        var prodIds = prodList.Select(p => p.Id).ToArray();
        var batchList = await _db.Batches
            .AsNoTracking()
            .Where(b => prodIds.Contains(b.ProductId))
            .OrderBy(b => b.ProductId).ThenBy(b => b.Register).ThenBy(b => b.CreatedAt)
            .ToListAsync(ct);

        var list = (from b in batchList
                    join p in prodList on b.ProductId equals p.Id
                    select new BatchStockViewDto
                    {
                        ProductId = p.Id,
                        Sku = p.Sku,
                        Name = p.Name,
                        Category = p.Category ?? string.Empty,
                        Register = b.Register.ToString(),
                        Code = b.Code,
                        Qty = b.Qty,
                        UnitCost = b.UnitCost,
                        CreatedAt = b.CreatedAt,
                        Note = b.Note
                    })
                   .ToList();

        return Ok(list);
    }
}
