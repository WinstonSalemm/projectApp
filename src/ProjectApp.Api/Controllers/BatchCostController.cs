using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectApp.Api.Models;
using ProjectApp.Api.Services;
using System.Security.Claims;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/batch-cost")]
[Authorize]
public class BatchCostController : ControllerBase
{
    private readonly BatchCostCalculationService _costService;

    public BatchCostController(BatchCostCalculationService costService)
    {
        _costService = costService;
    }

    /// <summary>
    /// Получить настройки расчета для поставки
    /// </summary>
    [HttpGet("settings/{supplyId}")]
    public async Task<ActionResult<BatchCostSettings>> GetSettings(int supplyId)
    {
        var settings = await _costService.GetOrCreateSettingsAsync(supplyId);
        return Ok(settings);
    }

    /// <summary>
    /// Обновить настройки расчета
    /// </summary>
    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] BatchCostSettings settings)
    {
        await _costService.UpdateSettingsAsync(settings);
        return Ok(new { message = "Настройки обновлены" });
    }

    /// <summary>
    /// Получить все товары в расчете
    /// </summary>
    [HttpGet("items/{supplyId}")]
    public async Task<ActionResult<List<BatchCostCalculation>>> GetItems(int supplyId)
    {
        var items = await _costService.GetItemsBySupplyAsync(supplyId);
        return Ok(items);
    }

    /// <summary>
    /// Добавить товар в расчет
    /// </summary>
    [HttpPost("items")]
    public async Task<IActionResult> AddItem([FromBody] AddBatchCostItemRequest request)
    {
        var settings = await _costService.GetOrCreateSettingsAsync(request.SupplyId);
        var userName = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";

        var item = await _costService.AddItemAsync(
            request.SupplyId,
            request.BatchId,
            request.ProductName,
            request.Quantity,
            request.PriceRub,
            settings,
            userName);

        // Пересчитываем все товары (для обновления фикс. сумм)
        await _costService.RecalculateAllAsync(request.SupplyId);

        return Ok(new { message = "Товар добавлен", itemId = item.Id });
    }

    /// <summary>
    /// Удалить товар из расчета
    /// </summary>
    [HttpDelete("items/{itemId}")]
    public async Task<IActionResult> DeleteItem(int itemId, [FromQuery] int supplyId)
    {
        await _costService.DeleteItemAsync(itemId, supplyId);
        return Ok(new { message = "Товар удален" });
    }

    /// <summary>
    /// Пересчитать всю партию
    /// </summary>
    [HttpPost("recalculate/{supplyId}")]
    public async Task<IActionResult> Recalculate(int supplyId)
    {
        await _costService.RecalculateAllAsync(supplyId);
        var total = await _costService.GetTotalCostAsync(supplyId);
        
        return Ok(new 
        { 
            message = "Расчет выполнен",
            totalCost = total
        });
    }

    /// <summary>
    /// Получить общую себестоимость партии
    /// </summary>
    [HttpGet("total/{supplyId}")]
    public async Task<ActionResult<decimal>> GetTotal(int supplyId)
    {
        var total = await _costService.GetTotalCostAsync(supplyId);
        return Ok(new { totalCost = total });
    }
}

public class AddBatchCostItemRequest
{
    public int SupplyId { get; set; }
    public int? BatchId { get; set; }  // Опционально, для новых товаров может быть null
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal PriceRub { get; set; }
}
