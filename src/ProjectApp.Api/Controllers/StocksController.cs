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
    private readonly ILogger<StocksController> _logger;
    
    public StocksController(AppDbContext db, ILogger<StocksController> logger) 
    { 
        _db = db;
        _logger = logger;
    }

    [HttpGet("test")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Test(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("[StocksController] Test: checking Stocks table");
            
            // Just load stocks without joining products
            var stocks = await _db.Stocks.AsNoTracking().Take(5).ToListAsync(ct);
            
            return Ok(new { success = true, count = stocks.Count, stocks });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StocksController] Test failed: {Message}", ex.Message);
            return Ok(new { success = false, error = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    [HttpGet("test-products")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> TestProducts(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("[StocksController] TestProducts: loading products with FromSqlRaw");
            
            var products = await _db.Products
                .FromSqlRaw("SELECT Id, Sku, Name, Unit, Price, Category FROM Products")
                .Take(5)
                .ToListAsync(ct);
            
            return Ok(new { success = true, count = products.Count, products });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StocksController] TestProducts failed: {Message}", ex.Message);
            return Ok(new { success = false, error = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<StockViewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] string? query, [FromQuery] string? category, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("[StocksController] Get: query={Query}, category={Category}", query, category);
            
            // Load all products first, then filter in memory to avoid GtdCode issue
            var allProducts = await _db.Products
                .FromSqlRaw("SELECT Id, Sku, Name, Unit, Price, Category FROM Products")
                .ToListAsync(ct);
            
            _logger.LogInformation("[StocksController] Loaded {Count} products from DB", allProducts.Count);
            
            var prodList = allProducts.AsQueryable();
            
            if (!string.IsNullOrWhiteSpace(category))
            {
                var c = category.Trim();
                prodList = prodList.Where(p => p.Category == c);
            }
            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.Trim().ToLower();
                prodList = prodList.Where(p => 
                    p.Sku.ToLower().Contains(q) || 
                    p.Name.ToLower().Contains(q));
            }
            
            var prodListFinal = prodList
                .OrderBy(p => p.Id)
                .Select(p => new { 
                    Id = p.Id, 
                    Sku = p.Sku, 
                    Name = p.Name, 
                    Category = p.Category 
                })
                .ToList();

        if (prodListFinal.Count == 0)
        {
            return Ok(Array.Empty<StockViewDto>());
        }

        var prodIds = prodListFinal.Select(p => p.Id).ToArray();
        var stockList = await _db.Stocks
            .AsNoTracking()
            .Where(s => prodIds.Contains(s.ProductId))
            .ToListAsync(ct);

        var grouped = stockList
            .GroupBy(s => s.ProductId)
            .ToDictionary(g => g.Key, g => new
            {
                Nd40 = g.Where(x => x.Register == StockRegister.ND40).Sum(x => x.Qty),
                Im40 = g.Where(x => x.Register == StockRegister.IM40).Sum(x => x.Qty),
                Total = g.Sum(x => x.Qty)
            });

        // TEMPORARY: Skip reservations to simplify
        var result = prodListFinal.Select(p => new StockViewDto
        {
            ProductId = p.Id,
            Sku = p.Sku,
            Name = p.Name,
            Category = p.Category ?? string.Empty,
            Nd40Qty = grouped.TryGetValue(p.Id, out var agg) ? agg.Nd40 : 0m,
            Im40Qty = grouped.TryGetValue(p.Id, out var agg2) ? agg2.Im40 : 0m,
            TotalQty = grouped.TryGetValue(p.Id, out var agg3) ? agg3.Total : 0m
        }).ToList();

            _logger.LogInformation("[StocksController] Get: returning {Count} items", result.Count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StocksController] Get failed: {Message}", ex.Message);
            throw;
        }
    }

    // GET /api/stocks/batches?query=&category=
    [HttpGet("batches")]
    [ProducesResponseType(typeof(IEnumerable<BatchStockViewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByBatches([FromQuery] string? query, [FromQuery] string? category, CancellationToken ct)
    {
        // Load all products using FromSqlRaw to avoid GtdCode issue
        var allProducts = await _db.Products
            .FromSqlRaw("SELECT Id, Sku, Name, Unit, Price, Category FROM Products")
            .ToListAsync(ct);
        
        var products = allProducts.AsQueryable();
        if (!string.IsNullOrWhiteSpace(category))
        {
            var c = category.Trim();
            products = products.Where(p => p.Category == c);
        }
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim().ToLower();
            products = products.Where(p => p.Sku.ToLower().Contains(q) || p.Name.ToLower().Contains(q));
        }

        var prodList = products
            .Select(p => new { 
                Id = p.Id, 
                Sku = p.Sku, 
                Name = p.Name, 
                Category = p.Category 
            })
            .OrderBy(p => p.Id)
            .ToList();

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
