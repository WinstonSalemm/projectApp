using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class SuppliesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<SuppliesController> _logger;

    public SuppliesController(AppDbContext db, ILogger<SuppliesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Получить список поставок с фильтром по типу регистра
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Supply>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] RegisterType? registerType, CancellationToken ct)
    {
        var query = _db.Supplies
            .Include(s => s.Items)
            .ThenInclude(i => i.Product)
            .AsQueryable();

        if (registerType.HasValue)
            query = query.Where(s => s.RegisterType == registerType.Value);

        // Сортировка: HasStock сверху, Finished внизу
        var supplies = await query
            .OrderBy(s => s.Status == SupplyStatus.Finished ? 1 : 0)
            .ThenByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        return Ok(supplies);
    }

    /// <summary>
    /// Получить поставку по ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Supply), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var supply = await _db.Supplies
            .Include(s => s.Items)
            .ThenInclude(i => i.Product)
            .Include(s => s.CostingSessions)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (supply == null)
            return NotFound();

        return Ok(supply);
    }

    /// <summary>
    /// Создать новую поставку (по умолчанию в ND-40)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Supply), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateSupplyDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest("Code (№ ГТД) is required");

        // Проверка уникальности кода
        var exists = await _db.Supplies.AnyAsync(s => s.Code == dto.Code, ct);
        if (exists)
            return BadRequest($"Supply with code '{dto.Code}' already exists");

        var supply = new Supply
        {
            Code = dto.Code,
            RegisterType = RegisterType.ND40, // всегда создаётся в ND-40
            Status = SupplyStatus.HasStock,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Supplies.Add(supply);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = supply.Id }, supply);
    }

    /// <summary>
    /// Обновить поставку
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSupplyDto dto, CancellationToken ct)
    {
        var supply = await _db.Supplies.FindAsync(new object[] { id }, ct);
        if (supply == null)
            return NotFound();

        // После перевода в IM-40 - read-only
        if (supply.RegisterType == RegisterType.IM40)
            return BadRequest("Cannot update supply after transfer to IM-40");

        if (!string.IsNullOrWhiteSpace(dto.Code))
        {
            // Проверка уникальности кода
            var codeExists = await _db.Supplies
                .AnyAsync(s => s.Code == dto.Code && s.Id != id, ct);
            if (codeExists)
                return BadRequest($"Supply with code '{dto.Code}' already exists");

            supply.Code = dto.Code;
        }

        supply.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Удалить поставку
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var supply = await _db.Supplies
            .Include(s => s.Items)
            .Include(s => s.CostingSessions)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (supply == null)
            return NotFound();

        // Удаляем связанные сущности
        _db.CostingItemSnapshots.RemoveRange(
            _db.CostingItemSnapshots.Where(cs => cs.CostingSessionId ==
                supply.CostingSessions.Select(s => s.Id).FirstOrDefault()));
        _db.CostingSessions.RemoveRange(supply.CostingSessions);
        _db.SupplyItems.RemoveRange(supply.Items);
        _db.Supplies.Remove(supply);

        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Перевод поставки целиком из ND-40 в IM-40
    /// </summary>
    [HttpPost("{id}/transfer-to-im40")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TransferToIm40(int id, CancellationToken ct)
    {
        var supply = await _db.Supplies.FindAsync(new object[] { id }, ct);
        if (supply == null)
            return NotFound();

        if (supply.RegisterType != RegisterType.ND40)
            return BadRequest("Supply is not in ND-40");

        // Перевод целиком
        supply.RegisterType = RegisterType.IM40;
        supply.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Изменить статус поставки
    /// </summary>
    [HttpPut("{id}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusDto dto, CancellationToken ct)
    {
        var supply = await _db.Supplies.FindAsync(new object[] { id }, ct);
        if (supply == null)
            return NotFound();

        supply.Status = dto.Status;
        supply.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}

// DTOs
public record CreateSupplyDto(string Code);
public record UpdateSupplyDto(string? Code);
public record UpdateStatusDto(SupplyStatus Status);
