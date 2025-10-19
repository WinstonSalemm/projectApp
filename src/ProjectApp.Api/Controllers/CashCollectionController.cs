using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectApp.Api.Services;

namespace ProjectApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/cash-collection")]
public class CashCollectionController : ControllerBase
{
    private readonly CashCollectionService _service;
    private readonly ILogger<CashCollectionController> _logger;

    public CashCollectionController(CashCollectionService service, ILogger<CashCollectionController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Получить сводку для страницы "К инкассации"
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<CashCollectionSummaryDto>> GetSummary()
    {
        try
        {
            var summary = await _service.GetSummaryAsync();
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cash collection summary");
            return StatusCode(500, "Ошибка получения данных");
        }
    }

    /// <summary>
    /// Провести инкассацию
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CashCollectionDto>> CreateCollection([FromBody] CreateCashCollectionDto dto)
    {
        try
        {
            if (dto.CollectedAmount < 0)
                return BadRequest("Сумма инкассации не может быть отрицательной");

            var userName = User.Identity?.Name ?? "Unknown";
            var result = await _service.CreateCollectionAsync(dto, userName);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating cash collection");
            return StatusCode(500, "Ошибка проведения инкассации");
        }
    }

    /// <summary>
    /// Получить историю инкассаций
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<List<CashCollectionDto>>> GetHistory(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        try
        {
            var history = await _service.GetHistoryAsync(from, to);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cash collection history");
            return StatusCode(500, "Ошибка получения истории");
        }
    }

    /// <summary>
    /// Удалить последнюю инкассацию (если была ошибка)
    /// </summary>
    [HttpDelete("last")]
    public async Task<ActionResult> DeleteLastCollection()
    {
        try
        {
            var deleted = await _service.DeleteLastCollectionAsync();
            if (!deleted)
                return NotFound("Нет инкассаций для удаления");

            return Ok("Последняя инкассация удалена");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting last cash collection");
            return StatusCode(500, "Ошибка удаления инкассации");
        }
    }
}
