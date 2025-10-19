using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/defectives")]
public class DefectivesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<DefectivesController> _logger;

    public DefectivesController(AppDbContext db, ILogger<DefectivesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Получить список брака
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<DefectiveItemDto>>> GetDefectives(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] DefectiveStatus? status = null)
    {
        try
        {
            var query = _db.Set<DefectiveItem>()
                .Include(d => d.Product)
                .AsQueryable();

            if (from.HasValue)
                query = query.Where(d => d.CreatedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(d => d.CreatedAt <= to.Value);

            if (status.HasValue)
                query = query.Where(d => d.Status == status.Value);

            var defectives = await query
                .OrderByDescending(d => d.CreatedAt)
                .Take(100)
                .ToListAsync();

            var dtos = defectives.Select(d => new DefectiveItemDto
            {
                Id = d.Id,
                ProductId = d.ProductId,
                ProductName = d.ProductName,
                Sku = d.Sku,
                Quantity = d.Quantity,
                Warehouse = d.Warehouse,
                Reason = d.Reason,
                Status = d.Status,
                CreatedBy = d.CreatedBy,
                CreatedAt = d.CreatedAt,
                CancelledBy = d.CancelledBy,
                CancelledAt = d.CancelledAt,
                CancellationReason = d.CancellationReason
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting defectives");
            return StatusCode(500, "Ошибка получения списка брака");
        }
    }

    /// <summary>
    /// Создать списание брака
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<DefectiveItemDto>> CreateDefective([FromBody] CreateDefectiveDto dto)
    {
        try
        {
            if (dto.Quantity <= 0)
                return BadRequest("Количество должно быть больше 0");

            // Проверить товар
            var product = await _db.Products.FindAsync(dto.ProductId);
            if (product == null)
                return NotFound("Товар не найден");

            // Проверить наличие на складе
            var stock = await _db.Stocks
                .Where(s => s.ProductId == dto.ProductId && s.Register == dto.Warehouse)
                .FirstOrDefaultAsync();

            if (stock == null || stock.Qty < dto.Quantity)
                return BadRequest($"Недостаточно товара на складе {dto.Warehouse}");

            var userName = User.Identity?.Name ?? "Unknown";

            // Создать запись брака
            var defective = new DefectiveItem
            {
                ProductId = dto.ProductId,
                ProductName = product.Name,
                Sku = product.Sku,
                Quantity = dto.Quantity,
                Warehouse = dto.Warehouse,
                Reason = dto.Reason,
                Status = DefectiveStatus.Active,
                CreatedBy = userName,
                CreatedAt = DateTime.UtcNow
            };

            _db.Set<DefectiveItem>().Add(defective);

            // Списать со склада
            stock.Qty -= dto.Quantity;

            // Записать транзакцию
            var transaction = new InventoryTransaction
            {
                ProductId = dto.ProductId,
                Register = dto.Warehouse,
                Type = InventoryTransactionType.Defective,
                Qty = -dto.Quantity,
                UnitCost = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName,
                Note = $"Брак: {dto.Reason}"
            };

            _db.Set<InventoryTransaction>().Add(transaction);

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Defective created: Product={ProductId}, Qty={Qty}, Warehouse={Warehouse}, By={User}",
                dto.ProductId, dto.Quantity, dto.Warehouse, userName);

            return Ok(new DefectiveItemDto
            {
                Id = defective.Id,
                ProductId = defective.ProductId,
                ProductName = defective.ProductName,
                Sku = defective.Sku,
                Quantity = defective.Quantity,
                Warehouse = defective.Warehouse,
                Reason = defective.Reason,
                Status = defective.Status,
                CreatedBy = defective.CreatedBy,
                CreatedAt = defective.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating defective");
            return StatusCode(500, "Ошибка создания списания брака");
        }
    }

    /// <summary>
    /// Отменить списание брака (вернуть на склад)
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<ActionResult> CancelDefective(int id, [FromBody] CancelDefectiveDto dto)
    {
        try
        {
            var defective = await _db.Set<DefectiveItem>().FindAsync(id);
            if (defective == null)
                return NotFound("Запись брака не найдена");

            if (defective.Status == DefectiveStatus.Cancelled)
                return BadRequest("Запись уже отменена");

            var userName = User.Identity?.Name ?? "Unknown";

            // Вернуть на склад
            var stock = await _db.Stocks
                .Where(s => s.ProductId == defective.ProductId && s.Register == defective.Warehouse)
                .FirstOrDefaultAsync();

            if (stock == null)
            {
                stock = new Stock
                {
                    ProductId = defective.ProductId,
                    Register = defective.Warehouse,
                    Qty = defective.Quantity
                };
                _db.Stocks.Add(stock);
            }
            else
            {
                stock.Qty += defective.Quantity;
            }

            // Обновить статус
            defective.Status = DefectiveStatus.Cancelled;
            defective.CancelledBy = userName;
            defective.CancelledAt = DateTime.UtcNow;
            defective.CancellationReason = dto.Reason;

            // Записать транзакцию возврата
            var transaction = new InventoryTransaction
            {
                ProductId = defective.ProductId,
                Register = defective.Warehouse,
                Type = InventoryTransactionType.DefectiveCancelled,
                Qty = defective.Quantity,
                UnitCost = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName,
                Note = $"Отмена брака: {dto.Reason}"
            };

            _db.Set<InventoryTransaction>().Add(transaction);

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Defective cancelled: Id={Id}, Product={ProductId}, Qty={Qty}, By={User}",
                id, defective.ProductId, defective.Quantity, userName);

            return Ok("Списание брака отменено");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling defective");
            return StatusCode(500, "Ошибка отмены списания брака");
        }
    }
}

#region DTOs

public class DefectiveItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? Sku { get; set; }
    public int Quantity { get; set; }
    public StockRegister Warehouse { get; set; }
    public string? Reason { get; set; }
    public DefectiveStatus Status { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string? CancelledBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}

public class CreateDefectiveDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public StockRegister Warehouse { get; set; } = StockRegister.ND40;
    public string? Reason { get; set; }
}

public class CancelDefectiveDto
{
    public string? Reason { get; set; }
}

#endregion
