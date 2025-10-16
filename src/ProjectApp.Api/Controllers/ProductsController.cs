using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectApp.Api.Models;
using ProjectApp.Api.Repositories;
using ProjectApp.Api.Dtos;
using System.Linq;
using ProjectApp.Api.Data;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductRepository _repository;
    private readonly AppDbContext _db;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(IProductRepository repository, AppDbContext db, ILogger<ProductsController> logger)
    {
        _repository = repository;
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductDto>>> Get([FromQuery] string? query = null, [FromQuery] int page = 1, [FromQuery] int size = 50, [FromQuery] string? category = null, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("[ProductsController] Get: query={Query}, page={Page}, size={Size}, category={Category}", query, page, size, category);
            if (page < 1) page = 1;
            if (size < 1) size = 50;

            var total = await _repository.CountAsync(query, category, ct);
            _logger.LogInformation("[ProductsController] CountAsync returned: {Total}", total);
            
            var items = await _repository.SearchAsync(query, page, size, category, ct);
            _logger.LogInformation("[ProductsController] SearchAsync returned: {Count} items", items?.Count() ?? 0);
            
            var dtoItems = items.Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                UnitPrice = p.Price,
                Price = p.Price,
                Cost = p.Cost,
                Category = p.Category
            }).ToList();

            var result = new PagedResult<ProductDto>
            {
                Items = dtoItems,
                Total = total,
                Page = page,
                Size = size
            };

            _logger.LogInformation("[ProductsController] Returning {Count} items", dtoItems.Count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ProductsController] Get failed: {Message}", ex.Message);
            throw;
        }
    }

    [HttpGet("categories")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<string>>> GetCategories(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("[ProductsController] GetCategories started");
            // Merge directory categories with those coming from existing products
            var repoCats = await _repository.GetCategoriesAsync(ct);
            _logger.LogInformation("[ProductsController] Repository categories: {Count}", repoCats?.Count() ?? 0);
            
            var dirCats = await _db.Categories.AsNoTracking().Select(c => c.Name)
                .Where(n => n != null && n != "")
                .ToListAsync(ct);
            _logger.LogInformation("[ProductsController] Directory categories: {Count}", dirCats.Count);
            
            var all = repoCats.Concat(dirCats)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(c => c)
                .ToList();
            _logger.LogInformation("[ProductsController] Returning {Count} categories", all.Count);
            return Ok(all);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ProductsController] GetCategories failed: {Message}", ex.Message);
            throw;
        }
    }

    [HttpGet("debug")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Debug(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("[ProductsController] Debug: checking Products table");
            
            // Try to get raw SQL result
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync(ct);
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Sku, Name, Unit, Price, Category FROM Products LIMIT 1";
            
            var reader = await cmd.ExecuteReaderAsync(ct);
            var result = new List<Dictionary<string, object?>>();
            
            while (await reader.ReadAsync(ct))
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                result.Add(row);
            }
            
            await reader.CloseAsync();
            await conn.CloseAsync();
            
            _logger.LogInformation("[ProductsController] Debug: got {Count} rows", result.Count);
            return Ok(new { success = true, rows = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ProductsController] Debug failed: {Message}", ex.Message);
            return Ok(new { success = false, error = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    [HttpGet("lookup")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Lookup([FromQuery] string ids, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ids)) return Ok(Array.Empty<object>());
        var parts = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                       .Select(p => int.TryParse(p, out var x) ? x : (int?)null)
                       .Where(x => x.HasValue)
                       .Select(x => x!.Value)
                       .Distinct()
                       .ToList();
        if (parts.Count == 0) return Ok(Array.Empty<object>());
        var list = await _db.Products.AsNoTracking().Where(p => parts.Contains(p.Id))
            .Select(p => new { p.Id, p.Sku, p.Name })
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductDto>> Create([FromBody] ProductCreateDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Sku) || string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Unit))
            return ValidationProblem(detail: "Sku, Name, Unit are required");

        var p = new Product
        {
            Sku = dto.Sku.Trim(),
            Name = dto.Name.Trim(),
            Unit = dto.Unit.Trim(),
            Price = dto.Price,
            Category = dto.Category?.Trim() ?? string.Empty
        };
        // Ensure category directory contains this name (optional directory)
        if (!string.IsNullOrWhiteSpace(p.Category))
        {
            var catName = p.Category.Trim();
            if (!await _db.Categories.AnyAsync(c => c.Name == catName, ct))
            {
                _db.Categories.Add(new CategoryRec { Name = catName });
                await _db.SaveChangesAsync(ct);
            }
        }

        p = await _repository.AddAsync(p, ct);
        var result = new ProductDto { Id = p.Id, Name = p.Name, Sku = p.Sku, UnitPrice = p.Price, Category = p.Category };
        return Created($"/api/products/{p.Id}", result);
    }

    [HttpPost("seed-standard")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SeedStandard(CancellationToken ct)
    {
        var items = new (string Category, string Sku, string Name)[]
        {
            ("Огнетушители ПОРОШКОВЫЕ", "ОП-2", "ОП-2"),
            ("Огнетушители ПОРОШКОВЫЕ", "ОП-3", "ОП-3"),
            ("Огнетушители ПОРОШКОВЫЕ", "ОП-4", "ОП-4"),

            ("Огнетушители УГЛЕКИСЛОТНЫЕ", "ОУ-2", "ОУ-2"),
            ("Огнетушители УГЛЕКИСЛОТНЫЕ", "ОУ-3", "ОУ-3"),
            ("Огнетушители УГЛЕКИСЛОТНЫЕ", "ОУ-4", "ОУ-4"),

            ("рукава", "РУКАВ 51-8", "РУКАВ 51-8"),
            ("рукава", "РУКАВ 51-10", "РУКАВ 51-10"),
            ("рукава", "РУКАВ 65-8", "РУКАВ 65-8"),
            ("рукава", "РУКАВ 65-10", "РУКАВ 65-10"),
            ("рукава", "РУКАВ 80-8бар", "РУКАВ 80-8бар"),

            ("кронштейны", "кронштейн МИГ", "кронштейн МИГ"),
            ("кронштейны", "кронштейн универсальный", "кронштейн универсальный"),

            ("подставки", "подставка п-15", "подставка п-15"),
            ("подставки", "подставка п-20", "подставка п-20"),
            ("подставки", "подставка п-25", "подставка п-25"),

            ("датчики", "ипр-513", "ипр-513"),
            ("датчики", "ипр-503", "ипр-503"),
            ("датчики", "glasstreck", "glasstreck"),

            ("шкафы", "шпк-15", "шпк-15"),
            ("шкафы", "шпк-20", "шпк-20"),
            ("шкафы", "шпк-25", "шпк-25"),
        };

        var existingSkus = await _db.Products.AsNoTracking().Select(p => p.Sku).ToListAsync(ct);
        var toAdd = items.Where(i => !existingSkus.Contains(i.Sku)).ToList();
        foreach (var i in toAdd)
        {
            _db.Products.Add(new Product
            {
                Sku = i.Sku,
                Name = i.Name,
                Unit = "шт",
                Price = 0m,
                Category = i.Category
            });
        }
        await _db.SaveChangesAsync(ct);

        return Ok(new { added = toAdd.Count, total = items.Length });
    }

    // PUT /api/products/{id}/cost - обновить себестоимость товара (только админ)
    [HttpPut("{id}/cost")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateCost(int id, [FromBody] UpdateCostRequest request, CancellationToken ct = default)
    {
        try
        {
            var product = await _db.Products.FindAsync(new object[] { id }, ct);
            if (product == null)
            {
                return NotFound(new { error = "Product not found" });
            }

            product.Cost = request.Cost;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("[ProductsController] Updated cost for product {ProductId}: {Cost}", id, request.Cost);
            return Ok(new { id = product.Id, sku = product.Sku, cost = product.Cost });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ProductsController] UpdateCost failed for product {ProductId}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    public class UpdateCostRequest
    {
        public decimal Cost { get; set; }
    }
}
