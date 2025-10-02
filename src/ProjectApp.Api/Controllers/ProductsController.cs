using Microsoft.AspNetCore.Mvc;
using ProjectApp.Api.Models;
using ProjectApp.Api.Repositories;
using ProjectApp.Api.Dtos;
using System.Linq;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductRepository _repository;

    public ProductsController(IProductRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductDto>>> Get([FromQuery] string? query = null, [FromQuery] int page = 1, [FromQuery] int size = 50, [FromQuery] string? category = null, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 50;

        var total = await _repository.CountAsync(query, category, ct);
        var items = await _repository.SearchAsync(query, page, size, category, ct);
        var dtoItems = items.Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Sku = p.Sku,
            UnitPrice = p.Price,
            Category = p.Category
        }).ToList();

        var result = new PagedResult<ProductDto>
        {
            Items = dtoItems,
            Total = total,
            Page = page,
            Size = size
        };

        return Ok(result);
    }

    [HttpGet("categories")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<string>>> GetCategories(CancellationToken ct)
    {
        var cats = await _repository.GetCategoriesAsync(ct);
        return Ok(cats);
    }

    [HttpPost]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminOnly")]
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
        p = await _repository.AddAsync(p, ct);
        var result = new ProductDto { Id = p.Id, Name = p.Name, Sku = p.Sku, UnitPrice = p.Price, Category = p.Category };
        return Created($"/api/products/{p.Id}", result);
    }
}
