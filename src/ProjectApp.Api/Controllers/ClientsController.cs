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
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(IEnumerable<Client>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] ClientType? type, [FromQuery] string? owner, [FromQuery] int page = 1, [FromQuery] int size = 50)
    {
        var query = db.Clients.AsNoTracking().AsQueryable();
        var isAdmin = User.IsInRole("Admin");
        // Default: managers see only their own; admin can see all
        var effectiveOwner = isAdmin ? owner : (User?.Identity?.Name ?? owner);
        if (!string.IsNullOrWhiteSpace(effectiveOwner))
            query = query.Where(c => c.OwnerUserName == effectiveOwner);
        if (type.HasValue) query = query.Where(c => c.Type == type.Value);
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
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(Client), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id)
    {
        var c = await db.Clients.FindAsync(id);
        if (c is null) return NotFound();
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin)
        {
            var user = User?.Identity?.Name;
            if (!string.Equals(c.OwnerUserName, user, StringComparison.OrdinalIgnoreCase)) return Forbid();
        }
        return Ok(c);
    }

    [HttpPost]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(Client), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] ClientCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return ValidationProblem("Name is required");
        var c = new Client
        {
            Name = dto.Name.Trim(),
            Phone = dto.Phone,
            Inn = dto.Inn,
            Type = dto.Type,
            OwnerUserName = User?.Identity?.Name,
            CreatedAt = DateTime.UtcNow
        };
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
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin)
        {
            var user = User?.Identity?.Name;
            if (!string.Equals(c.OwnerUserName, user, StringComparison.OrdinalIgnoreCase)) return Forbid();
        }
        if (!string.IsNullOrWhiteSpace(dto.Name)) c.Name = dto.Name.Trim();
        c.Phone = dto.Phone; c.Inn = dto.Inn;
        if (dto.Type.HasValue) c.Type = dto.Type.Value;
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

    // ---- Client history endpoints ----
    [HttpGet("{id:int}/sales")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(IEnumerable<Sale>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSales(int id, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (client is null) return NotFound();
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin)
        {
            var user = User?.Identity?.Name;
            if (!string.Equals(client.OwnerUserName, user, StringComparison.OrdinalIgnoreCase)) return Forbid();
        }
        var q = db.Sales.AsNoTracking().Where(s => s.ClientId == id);
        if (from.HasValue) q = q.Where(s => s.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(s => s.CreatedAt < to.Value);
        var list = await q.OrderByDescending(s => s.Id).ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:int}/returns")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(IEnumerable<Return>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReturns(int id, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (client is null) return NotFound();
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin)
        {
            var user = User?.Identity?.Name;
            if (!string.Equals(client.OwnerUserName, user, StringComparison.OrdinalIgnoreCase)) return Forbid();
        }
        var q = db.Returns.AsNoTracking().Where(r => r.ClientId == id);
        if (from.HasValue) q = q.Where(r => r.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(r => r.CreatedAt < to.Value);
        var list = await q.OrderByDescending(r => r.Id).Include(r => r.Items).ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:int}/debts")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(IEnumerable<Debt>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDebts(int id, [FromQuery] DebtStatus? status, CancellationToken ct)
    {
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (client is null) return NotFound();
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin)
        {
            var user = User?.Identity?.Name;
            if (!string.Equals(client.OwnerUserName, user, StringComparison.OrdinalIgnoreCase)) return Forbid();
        }
        var q = db.Debts.AsNoTracking().Where(d => d.ClientId == id);
        if (status.HasValue) q = q.Where(d => d.Status == status.Value);
        var list = await q.OrderByDescending(d => d.DueDate).ThenBy(d => d.Id).ToListAsync(ct);
        return Ok(list);
    }

    // ---- Unregistered (anonymous) bucket ----
    [HttpGet("unregistered/sales")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(IEnumerable<Sale>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnregisteredSales([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var q = db.Sales.AsNoTracking().Where(s => s.ClientId == null);
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin)
        {
            var user = User?.Identity?.Name;
            q = q.Where(s => s.CreatedBy == user);
        }
        if (from.HasValue) q = q.Where(s => s.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(s => s.CreatedAt < to.Value);
        var list = await q.OrderByDescending(s => s.Id).ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("unregistered/returns")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(IEnumerable<Return>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnregisteredReturns([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var q = db.Returns.AsNoTracking().Where(r => r.ClientId == null);
        if (from.HasValue) q = q.Where(r => r.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(r => r.CreatedAt < to.Value);
        var list = await q.OrderByDescending(r => r.Id).Include(r => r.Items).ToListAsync(ct);
        return Ok(list);
    }
}
