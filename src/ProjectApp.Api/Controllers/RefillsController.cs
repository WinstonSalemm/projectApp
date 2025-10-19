using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/refills")]
public class RefillsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<RefillsController> _logger;

    public RefillsController(AppDbContext db, ILogger<RefillsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Получить список перезарядок
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<RefillOperationDto>>> GetRefills(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] RefillStatus? status = null)
    {
        try
        {
            var query = _db.Set<RefillOperation>()
                .Include(r => r.Product)
                .AsQueryable();

            if (from.HasValue)
                query = query.Where(r => r.CreatedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(r => r.CreatedAt <= to.Value);

            if (status.HasValue)
                query = query.Where(r => r.Status == status.Value);

            var refills = await query
                .OrderByDescending(r => r.CreatedAt)
                .Take(100)
                .ToListAsync();

            var dtos = refills.Select(r => new RefillOperationDto
            {
                Id = r.Id,
                ProductId = r.ProductId,
                ProductName = r.ProductName,
                Sku = r.Sku,
                Quantity = r.Quantity,
                Warehouse = r.Warehouse,
                CostPerUnit = r.CostPerUnit,
                TotalCost = r.TotalCost,
                Notes = r.Notes,
                Status = r.Status,
                CreatedBy = r.CreatedBy,
                CreatedAt = r.CreatedAt,
                CancelledBy = r.CancelledBy,
                CancelledAt = r.CancelledAt,
                CancellationReason = r.CancellationReason
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting refills");
            return StatusCode(500, "Ошибка получения списка перезарядок");
        }
    }

    /// <summary>
    /// Создать перезарядку
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RefillOperationDto>> CreateRefill([FromBody] CreateRefillDto dto)
    {
        try
        {
            if (dto.Quantity <= 0)
                return BadRequest("Количество должно быть больше 0");

            if (dto.CostPerUnit < 0)
                return BadRequest("Стоимость не может быть отрицательной");

            // Проверить товар
            var product = await _db.Products.FindAsync(dto.ProductId);
            if (product == null)
                return NotFound("Товар не найден");

            var userName = User.Identity?.Name ?? "Unknown";
            var totalCost = dto.Quantity * dto.CostPerUnit;

            // Создать запись перезарядки
            var refill = new RefillOperation
            {
                ProductId = dto.ProductId,
                ProductName = product.Name,
                Sku = product.Sku,
                Quantity = dto.Quantity,
                Warehouse = dto.Warehouse,
                CostPerUnit = dto.CostPerUnit,
                TotalCost = totalCost,
                Notes = dto.Notes,
                Status = RefillStatus.Active,
                CreatedBy = userName,
                CreatedAt = DateTime.UtcNow
            };

            _db.Set<RefillOperation>().Add(refill);

            // Записать транзакцию (информационная, не влияет на остатки)
            var transaction = new InventoryTransaction
            {
                ProductId = dto.ProductId,
                Register = dto.Warehouse,
                Type = InventoryTransactionType.Refill,
                Qty = dto.Quantity,
                UnitCost = dto.CostPerUnit,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName,
                Note = $"Перезарядка: {dto.Notes}"
            };

            _db.Set<InventoryTransaction>().Add(transaction);

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Refill created: Product={ProductId}, Qty={Qty}, Cost={Cost}, Warehouse={Warehouse}, By={User}",
                dto.ProductId, dto.Quantity, totalCost, dto.Warehouse, userName);

            return Ok(new RefillOperationDto
            {
                Id = refill.Id,
                ProductId = refill.ProductId,
                ProductName = refill.ProductName,
                Sku = refill.Sku,
                Quantity = refill.Quantity,
                Warehouse = refill.Warehouse,
                CostPerUnit = refill.CostPerUnit,
                TotalCost = refill.TotalCost,
                Notes = refill.Notes,
                Status = refill.Status,
                CreatedBy = refill.CreatedBy,
                CreatedAt = refill.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating refill");
            return StatusCode(500, "Ошибка создания перезарядки");
        }
    }

    /// <summary>
    /// Отменить перезарядку
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<ActionResult> CancelRefill(int id, [FromBody] CancelRefillDto dto)
    {
        try
        {
            var refill = await _db.Set<RefillOperation>().FindAsync(id);
            if (refill == null)
                return NotFound("Запись перезарядки не найдена");

            if (refill.Status == RefillStatus.Cancelled)
                return BadRequest("Запись уже отменена");

            var userName = User.Identity?.Name ?? "Unknown";

            // Обновить статус
            refill.Status = RefillStatus.Cancelled;
            refill.CancelledBy = userName;
            refill.CancelledAt = DateTime.UtcNow;
            refill.CancellationReason = dto.Reason;

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Refill cancelled: Id={Id}, Product={ProductId}, Qty={Qty}, By={User}",
                id, refill.ProductId, refill.Quantity, userName);

            return Ok("Перезарядка отменена");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling refill");
            return StatusCode(500, "Ошибка отмены перезарядки");
        }
    }

    /// <summary>
    /// Статистика по перезарядкам
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<RefillStatsDto>> GetStats(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        try
        {
            var query = _db.Set<RefillOperation>()
                .Where(r => r.Status == RefillStatus.Active);

            if (from.HasValue)
                query = query.Where(r => r.CreatedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(r => r.CreatedAt <= to.Value);

            var refills = await query.ToListAsync();

            var stats = new RefillStatsDto
            {
                TotalRefills = refills.Count,
                TotalQuantity = refills.Sum(r => r.Quantity),
                TotalCost = refills.Sum(r => r.TotalCost),
                AverageCostPerUnit = refills.Any() ? refills.Average(r => r.CostPerUnit) : 0
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting refill stats");
            return StatusCode(500, "Ошибка получения статистики");
        }
    }
}

#region DTOs

public class RefillOperationDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? Sku { get; set; }
    public int Quantity { get; set; }
    public StockRegister Warehouse { get; set; }
    public decimal CostPerUnit { get; set; }
    public decimal TotalCost { get; set; }
    public string? Notes { get; set; }
    public RefillStatus Status { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string? CancelledBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}

public class CreateRefillDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public StockRegister Warehouse { get; set; } = StockRegister.ND40;
    public decimal CostPerUnit { get; set; }
    public string? Notes { get; set; }
}

public class CancelRefillDto
{
    public string? Reason { get; set; }
}

public class RefillStatsDto
{
    public int TotalRefills { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalCost { get; set; }
    public decimal AverageCostPerUnit { get; set; }
}

#endregion
