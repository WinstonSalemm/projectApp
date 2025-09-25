using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Client>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int size = 50)
    {
        var query = db.Clients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            query = query.Where(c => EF.Functions.Like(c.Name, $"%{s}%") || EF.Functions.Like(c.Phone ?? "", $"%{s}%"));
        }
        page = Math.Max(1, page); size = Math.Clamp(size, 1, 200);
        var total = await query.CountAsync();
        var items = await query.OrderBy(c => c.Id).Skip((page-1)*size).Take(size).ToListAsync();
        return Ok(new { items, total, page, size });
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Client), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id)
    {
        var c = await db.Clients.FindAsync(id);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(Client), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] ClientCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return ValidationProblem("Name is required");
        var c = new Client { Name = dto.Name.Trim(), Phone = dto.Phone, Inn = dto.Inn };
        db.Clients.Add(c);
        await db.SaveChangesAsync();
        return Created($"/api/clients/{c.Id}", c);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(Client), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] ClientUpdateDto dto)
    {
        var c = await db.Clients.FindAsync(id);
        if (c is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(dto.Name)) c.Name = dto.Name.Trim();
        c.Phone = dto.Phone; c.Inn = dto.Inn;
        await db.SaveChangesAsync();
        return Ok(c);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await db.Clients.FindAsync(id);
        if (c is null) return NotFound();
        db.Clients.Remove(c);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
