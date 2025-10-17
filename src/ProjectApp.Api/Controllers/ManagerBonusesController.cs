using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectApp.Api.Models;
using ProjectApp.Api.Services;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class ManagerBonusesController : ControllerBase
{
    private readonly ManagerBonusService _bonusService;
    private readonly ILogger<ManagerBonusesController> _logger;

    public ManagerBonusesController(ManagerBonusService bonusService, ILogger<ManagerBonusesController> logger)
    {
        _bonusService = bonusService;
        _logger = logger;
    }

    /// <summary>
    /// Рассчитать бонусы за месяц
    /// </summary>
    [HttpPost("calculate")]
    [ProducesResponseType(typeof(List<ManagerBonus>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Calculate([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        try
        {
            var bonuses = await _bonusService.CalculateBonusesAsync(year, month, ct);
            await _bonusService.SaveBonusesAsync(bonuses, ct);
            return Ok(bonuses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ManagerBonusesController] Error calculating bonuses");
            return Problem(detail: ex.Message);
        }
    }

    /// <summary>
    /// Получить бонусы за месяц
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ManagerBonus>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBonuses([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        try
        {
            var bonuses = await _bonusService.GetBonusesAsync(year, month, ct);
            return Ok(bonuses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ManagerBonusesController] Error getting bonuses");
            return Problem(detail: ex.Message);
        }
    }

    /// <summary>
    /// Отметить бонус как выплаченный
    /// </summary>
    [HttpPost("{id}/mark-paid")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAsPaid([FromRoute] int id, CancellationToken ct)
    {
        try
        {
            await _bonusService.MarkAsPaidAsync(id, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ManagerBonusesController] Error marking bonus as paid");
            return Problem(detail: ex.Message);
        }
    }
}
