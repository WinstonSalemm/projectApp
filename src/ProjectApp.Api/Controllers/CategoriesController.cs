using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;
    public CategoriesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<string>>> GetAll(CancellationToken ct)
    {
        var names = await _db.Categories.AsNoTracking()
            .Select(c => c.Name)
            .Where(n => n != null && n != "")
            .OrderBy(n => n)
            .ToListAsync(ct);
        return Ok(names);
    }

    public class CreateCategoryDto { public string Name { get; set; } = string.Empty; }

    [HttpPost]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCategoryDto dto, CancellationToken ct)
    {
        var name = dto?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return ValidationProblem(detail: "Name is required");
        var exists = await _db.Categories.AnyAsync(c => c.Name == name, ct);
        if (!exists)
        {
            _db.Categories.Add(new CategoryRec { Name = name });
            await _db.SaveChangesAsync(ct);
        }
        return Created($"/api/categories/{Uri.EscapeDataString(name)}", new { name });
    }
}
