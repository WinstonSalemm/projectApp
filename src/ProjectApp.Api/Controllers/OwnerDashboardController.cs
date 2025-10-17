using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectApp.Api.Services;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OwnerDashboardController : ControllerBase
{
    private readonly OwnerDashboardService _service;

    public OwnerDashboardController(OwnerDashboardService service)
    {
        _service = service;
    }

    /// <summary>
    /// Получить главный дашборд владельца
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(OwnerDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard([FromQuery] DateTime? date = null)
    {
        var dashboard = await _service.GetDashboardAsync(date);
        return Ok(dashboard);
    }

    /// <summary>
    /// Получить P&L отчет (прибыли и убытки)
    /// </summary>
    [HttpGet("pl-report")]
    [ProducesResponseType(typeof(ProfitLossReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfitLossReport(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var report = await _service.GetProfitLossReportAsync(from, to);
        return Ok(report);
    }

    /// <summary>
    /// Получить Cash Flow отчет
    /// </summary>
    [HttpGet("cashflow-report")]
    [ProducesResponseType(typeof(CashFlowReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCashFlowReport(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var report = await _service.GetCashFlowReportAsync(from, to);
        return Ok(report);
    }
}
