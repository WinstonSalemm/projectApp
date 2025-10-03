using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class ContractsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ContractsController(AppDbContext db) { _db = db; }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ContractDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
    {
        var q = _db.Contracts.AsNoTracking().Include(c => c.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ContractStatus>(status, true, out var st))
            q = q.Where(c => c.Status == st);

        var list = await q.OrderByDescending(c => c.CreatedAt)
            .Select(c => new ContractDto
            {
                Id = c.Id,
                OrgName = c.OrgName,
                Inn = c.Inn,
                Phone = c.Phone,
                Status = c.Status.ToString(),
                CreatedAt = c.CreatedAt,
                Note = c.Note,
                Items = c.Items.Select(i => new ContractItemDto
                {
                    ProductId = i.ProductId,
                    Name = i.Name,
                    Unit = i.Unit,
                    Qty = i.Qty,
                    UnitPrice = i.UnitPrice
                }).ToList()
            }).ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken ct)
    {
        var c = await _db.Contracts.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();
        var dto = new ContractDto
        {
            Id = c.Id,
            OrgName = c.OrgName,
            Inn = c.Inn,
            Phone = c.Phone,
            Status = c.Status.ToString(),
            CreatedAt = c.CreatedAt,
            Note = c.Note,
            Items = c.Items.Select(i => new ContractItemDto
            {
                ProductId = i.ProductId,
                Name = i.Name,
                Unit = i.Unit,
                Qty = i.Qty,
                UnitPrice = i.UnitPrice
            }).ToList()
        };
        return Ok(dto);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] ContractCreateDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.OrgName))
            return ValidationProblem(detail: "OrgName is required");

        var status = Enum.TryParse<ContractStatus>(dto.Status, true, out var st) ? st : ContractStatus.Signed;
        var c = new Contract
        {
            OrgName = dto.OrgName.Trim(),
            Inn = string.IsNullOrWhiteSpace(dto.Inn) ? null : dto.Inn.Trim(),
            Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
            Status = status,
            CreatedAt = DateTime.UtcNow,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
            Items = dto.Items.Select(i => new ContractItem
            {
                ProductId = i.ProductId,
                Name = string.IsNullOrWhiteSpace(i.Name) ? string.Empty : i.Name.Trim(),
                Unit = string.IsNullOrWhiteSpace(i.Unit) ? "шт" : i.Unit.Trim(),
                Qty = i.Qty,
                UnitPrice = i.UnitPrice
            }).ToList()
        };
        _db.Contracts.Add(c);
        await _db.SaveChangesAsync(ct);

        return Created($"/api/contracts/{c.Id}", new { id = c.Id });
    }

    [HttpPut("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus([FromRoute] int id, [FromBody] ContractUpdateStatusDto dto, CancellationToken ct)
    {
        var c = await _db.Contracts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();
        if (!Enum.TryParse<ContractStatus>(dto.Status, true, out var st))
            return ValidationProblem(detail: "Invalid status");
        c.Status = st;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
