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
            // Загружаем поставку
            var supply = await _db.Supplies.FindAsync(new object[] { supplyId }, ct);
            if (supply == null)
                return NotFound($"Supply {supplyId} not found");

            // Валидация входных данных
            var nameRaw = dto.Name?.Trim();
            if (string.IsNullOrWhiteSpace(nameRaw))
                return BadRequest("Название товара обязательно");
            if (nameRaw.Length > 256) nameRaw = nameRaw.Substring(0, 256);
            if (dto.Quantity <= 0)
                return BadRequest("Количество должно быть больше 0");
            if (dto.PriceRub < 0)
                return BadRequest("Цена не может быть отрицательной");

            // Проверяем существование продукта по названию
            var product = await _db.Products
                .FirstOrDefaultAsync(p => p.Name.ToLower() == nameRaw.ToLower(), ct);

            if (product == null)
            {
                // Создаём новый продукт
                var sku = string.IsNullOrWhiteSpace(dto.Sku) ? $"AUTO-{Guid.NewGuid().ToString("N")[..8].ToUpper()}" : dto.Sku.Trim();
                if (sku.Length > 64) sku = sku.Substring(0, 64);

                product = new Product
                {
                    Name = nameRaw,
                    Category = string.IsNullOrWhiteSpace(dto.Category) ? "Другое" : dto.Category!.Trim(),
                    Sku = sku,
                    Unit = string.IsNullOrWhiteSpace("шт") ? "шт" : "шт",
                    Price = 0m,
                    Cost = 0m
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
                Sku = string.IsNullOrWhiteSpace(dto.Sku) ? product.Sku : dto.Sku!.Trim(),
                Quantity = dto.Quantity,
                PriceRub = dto.PriceRub,
                Weight = dto.Weight ?? 0
            };

            _db.SupplyItems.Add(item);
            await _db.SaveChangesAsync(ct);

            // ✅ Создаём партию на складе - товар сразу доступен
            if (supply != null)
            {
                var register = supply.RegisterType == RegisterType.ND40 ? StockRegister.ND40 : StockRegister.IM40;

                // Временная себестоимость = цена в рублях × курс
                var tempUnitCost = dto.PriceRub * 158.08m;

                var batch = new Batch
                {
                    ProductId = product.Id,
                    Register = register,
                    Qty = dto.Quantity,
                    UnitCost = tempUnitCost,
                    CreatedAt = DateTime.UtcNow,
                    Code = supply.Code,
                    Note = $"Из поставки {supply.Code}",
                    PurchaseSource = $"SupplyId:{supply.Id}",
                    PurchaseDate = supply.CreatedAt
                };

                _db.Batches.Add(batch);

                // Синхронизируем агрегированные остатки
                var stock = await _db.Stocks
                    .FirstOrDefaultAsync(s => s.ProductId == product.Id && s.Register == register, ct);

                if (stock == null)
                {
                    stock = new Stock
                    {
                        ProductId = product.Id,
                        Register = register,
                        Qty = 0
                    };
                    _db.Stocks.Add(stock);
                }

                stock.Qty += dto.Quantity;

                await _db.SaveChangesAsync(ct);

                // TODO: InventoryTransaction временно отключена для отладки
            }

            // Загружаем с Product для ответа (НЕ загружаем Supply чтобы избежать циклической ссылки)
            await _db.Entry(item).Reference(i => i.Product).LoadAsync(ct);
            
            // Обнуляем навигационное свойство Supply чтобы избежать циклов
            item.Supply = null;

            return CreatedAtAction(nameof(GetAll), new { supplyId }, item);
        }
        catch (Exception ex)
        {
            var details = new
            {
                error = ex.Message,
                innerError = ex.InnerException?.Message,
                innerInnerError = ex.InnerException?.InnerException?.Message,
                type = ex.GetType().Name,
                stack = ex.StackTrace?.Split('\n').Take(5).ToArray()
            };
            
            Console.WriteLine($"[ERROR] Add item failed: {ex}");
            return StatusCode(500, details);
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
        try
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

            // ✅ Удаляем соответствующую партию со склада (если она не используется)
            var register = supply.RegisterType == RegisterType.ND40 ? StockRegister.ND40 : StockRegister.IM40;

            var batch = await _db.Batches
                .Where(b => b.ProductId == item.ProductId
                         && b.Register == register
                         && b.PurchaseSource == $"SupplyId:{supply.Id}")
                .OrderByDescending(b => b.Id)
                .FirstOrDefaultAsync(ct);

            if (batch != null)
            {
                var hasRefs = await _db.InventoryConsumptions.AnyAsync(x => x.BatchId == batch.Id, ct)
                              || await _db.ReservationItemBatches.AnyAsync(x => x.BatchId == batch.Id, ct)
                              || await _db.ReturnItemRestocks.AnyAsync(x => x.BatchId == batch.Id, ct);
                if (hasRefs)
                {
                    return BadRequest("Нельзя удалить позицию: партия уже использована в продажах/резервациях/возвратах");
                }

                // Списываем агрегированные остатки
                var stock = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == item.ProductId && s.Register == register, ct);
                if (stock != null)
                {
                    stock.Qty -= item.Quantity;
                    if (stock.Qty < 0) stock.Qty = 0;
                }

                _db.Batches.Remove(batch);
            }

            _db.SupplyItems.Remove(item);
            await _db.SaveChangesAsync(ct);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete supply item failed: supply {SupplyId} item {ItemId}", supplyId, itemId);
            var details = new
            {
                error = ex.Message,
                innerError = ex.InnerException?.Message,
                type = ex.GetType().Name,
                stack = ex.StackTrace?.Split('\n').Take(5).ToArray()
            };
            return StatusCode(500, details);
        }
    }
}

// DTOs
public record AddSupplyItemDto(string Name, int Quantity, decimal PriceRub, string? Category = null, string? Sku = null, decimal? Weight = null);
public record UpdateSupplyItemDto(int? Quantity, decimal? PriceRub);
