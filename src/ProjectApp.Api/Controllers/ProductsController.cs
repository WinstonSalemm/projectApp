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
    public async Task<ActionResult<PagedResult<ProductDto>>> Get([FromQuery] string? query = null, [FromQuery] int page = 1, [FromQuery] int size = 50, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 50;

        var total = await _repository.CountAsync(query, ct);
        var items = await _repository.SearchAsync(query, page, size, ct);
        var dtoItems = items.Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Sku = p.Sku,
            UnitPrice = p.Price
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
}
