using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectApp.Api.Services;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/audit-log")]
[Authorize(Policy = "RequireApiKey")]
public class AuditLogController : ControllerBase
{
    private readonly AuditLogService _auditService;

    public AuditLogController(AuditLogService auditService)
    {
        _auditService = auditService;
    }

    /// <summary>
    /// Получить логи действий пользователя
    /// </summary>
    [HttpGet("user/{userName}")]
    public async Task<IActionResult> GetUserLogs(string userName, [FromQuery] int limit = 100)
    {
        var logs = await _auditService.GetUserLogsAsync(userName, limit);
        return Ok(logs);
    }

    /// <summary>
    /// Получить логи по сущности
    /// </summary>
    [HttpGet("entity/{entityType}/{entityId}")]
    public async Task<IActionResult> GetEntityLogs(string entityType, int entityId)
    {
        var logs = await _auditService.GetEntityLogsAsync(entityType, entityId);
        return Ok(logs);
    }

    /// <summary>
    /// Получить логи за период
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int limit = 1000)
    {
        var logs = await _auditService.GetLogsAsync(from, to, limit);
        return Ok(logs);
    }

    /// <summary>
    /// Статистика по действиям за период
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetActionStats(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var stats = await _auditService.GetActionStatsAsync(from, to);
        return Ok(stats);
    }
}
