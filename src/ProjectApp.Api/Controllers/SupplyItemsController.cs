using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/supplies/{supplyId}/items")]
[Authorize(Policy = "AdminOnly")]
public class SupplyItemsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<SupplyItemsController> _logger;

    public SupplyItemsController(AppDbContext db, ILogger<SupplyItemsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Получить все позиции поставки
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SupplyItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(int supplyId, CancellationToken ct)
    {
        var items = await _db.SupplyItems
            .Include(i => i.Product)
            .Where(i => i.SupplyId == supplyId)
            .ToListAsync(ct);

        return Ok(items);
    }

    /// <summary>
    /// Добавить позицию в поставку
    /// Если продукт с таким названием существует - используем его ID
    /// Иначе создаём новый продукт
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SupplyItem), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Add(int supplyId, [FromBody] AddSupplyItemDto dto, CancellationToken ct)
    {
        try
        {
            // Проверяем существование продукта по названию
            var product = await _db.Products
                .FirstOrDefaultAsync(p => p.Name.ToLower() == dto.Name.ToLower(), ct);

            if (product == null)
            {
                // Создаём новый продукт
                product = new Product
                {
                    Name = dto.Name,
                    Category = dto.Category ?? "Другое"
                };
                _db.Products.Add(product);
                await _db.SaveChangesAsync(ct);
            }

            // Создаём позицию
            var item = new SupplyItem
            {
                SupplyId = supplyId,
                ProductId = product.Id,
                Name = product.Name,
                Sku = dto.Sku ?? string.Empty,
                Quantity = dto.Quantity,
                PriceRub = dto.PriceRub,
                Weight = dto.Weight ?? 0
            };

            _db.SupplyItems.Add(item);
            await _db.SaveChangesAsync(ct);

            // Загружаем с Product для ответа
            await _db.Entry(item).Reference(i => i.Product).LoadAsync(ct);

            return CreatedAtAction(nameof(GetAll), new { supplyId }, item);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, innerError = ex.InnerException?.Message, stack = ex.StackTrace });
        }
    }

    /// <summary>
    /// Обновить позицию
    /// </summary>
    [HttpPut("{itemId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int supplyId, int itemId, [FromBody] UpdateSupplyItemDto dto, CancellationToken ct)
    {
        var supply = await _db.Supplies.FindAsync(new object[] { supplyId }, ct);
        if (supply == null)
            return NotFound("Supply not found");

        // После перевода в IM-40 - read-only
        if (supply.RegisterType == RegisterType.IM40)
            return BadRequest("Cannot update items after transfer to IM-40");

        var item = await _db.SupplyItems.FindAsync(new object[] { itemId }, ct);
        if (item == null || item.SupplyId != supplyId)
            return NotFound("Item not found");

        if (dto.Quantity.HasValue)
            item.Quantity = dto.Quantity.Value;

        if (dto.PriceRub.HasValue)
            item.PriceRub = dto.PriceRub.Value;

        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Удалить позицию
    /// </summary>
    [HttpDelete("{itemId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int supplyId, int itemId, CancellationToken ct)
    {
        var supply = await _db.Supplies.FindAsync(new object[] { supplyId }, ct);
        if (supply == null)
            return NotFound("Supply not found");

        // После перевода в IM-40 - read-only
        if (supply.RegisterType == RegisterType.IM40)
            return BadRequest("Cannot delete items after transfer to IM-40");

        var item = await _db.SupplyItems.FindAsync(new object[] { itemId }, ct);
        if (item == null || item.SupplyId != supplyId)
            return NotFound("Item not found");

        // TODO: Удаление Batch временно отключено

        _db.SupplyItems.Remove(item);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}

// DTOs
public record AddSupplyItemDto(string Name, int Quantity, decimal PriceRub, string? Category = null, string? Sku = null, decimal? Weight = null);
public record UpdateSupplyItemDto(int? Quantity, decimal? PriceRub);
