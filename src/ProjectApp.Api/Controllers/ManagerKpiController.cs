using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectApp.Api.Services;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/manager-kpi")]
[Authorize(Policy = "RequireApiKey")]
public class ManagerKpiController : ControllerBase
{
    private readonly ManagerKpiService _kpiService;

    public ManagerKpiController(ManagerKpiService kpiService)
    {
        _kpiService = kpiService;
    }

    /// <summary>
    /// Получить KPI всех менеджеров за период
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllKpi(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var kpi = await _kpiService.GetAllManagersKpiAsync(from, to);
        return Ok(kpi);
    }

    /// <summary>
    /// Получить KPI конкретного менеджера
    /// </summary>
    [HttpGet("{managerUserName}")]
    public async Task<IActionResult> GetManagerKpi(
        string managerUserName,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var kpi = await _kpiService.GetManagerKpiAsync(managerUserName, from, to);
        if (kpi == null)
            return NotFound();
        
        return Ok(kpi);
    }

    /// <summary>
    /// Получить топ менеджеров
    /// </summary>
    [HttpGet("top")]
    public async Task<IActionResult> GetTopManagers(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int top = 5)
    {
        var managers = await _kpiService.GetTopManagersAsync(from, to, top);
        return Ok(managers);
    }
}
