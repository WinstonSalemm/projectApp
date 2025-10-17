using ProjectApp.Api.Data;
using ProjectApp.Api.Models;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace ProjectApp.Api.Services;

/// <summary>
/// Сервис для логирования действий пользователей
/// </summary>
public class AuditLogService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AuditLogService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogService(
        AppDbContext db,
        ILogger<AuditLogService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Залогировать действие
    /// </summary>
    public async Task LogAsync(
        string userName,
        string action,
        string entityType,
        int? entityId = null,
        object? oldValue = null,
        object? newValue = null,
        string? details = null)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();
            var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString();

            var log = new AuditLog
            {
                UserName = userName,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                OldValue = oldValue != null ? JsonSerializer.Serialize(oldValue) : null,
                NewValue = newValue != null ? JsonSerializer.Serialize(newValue) : null,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Details = details,
                CreatedAt = DateTime.UtcNow
            };

            _db.Set<AuditLog>().Add(log);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Ошибка логирования действия: {action} {entityType} {entityId}");
        }
    }

    /// <summary>
    /// Получить логи действий пользователя
    /// </summary>
    public async Task<List<AuditLog>> GetUserLogsAsync(string userName, int limit = 100)
    {
        return await _db.Set<AuditLog>()
            .Where(l => l.UserName == userName)
            .OrderByDescending(l => l.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// Получить логи по сущности
    /// </summary>
    public async Task<List<AuditLog>> GetEntityLogsAsync(string entityType, int entityId)
    {
        return await _db.Set<AuditLog>()
            .Where(l => l.EntityType == entityType && l.EntityId == entityId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Получить все логи за период
    /// </summary>
    public async Task<List<AuditLog>> GetLogsAsync(DateTime from, DateTime to, int limit = 1000)
    {
        return await _db.Set<AuditLog>()
            .Where(l => l.CreatedAt >= from && l.CreatedAt < to)
            .OrderByDescending(l => l.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// Статистика по действиям
    /// </summary>
    public async Task<Dictionary<string, int>> GetActionStatsAsync(DateTime from, DateTime to)
    {
        return await _db.Set<AuditLog>()
            .Where(l => l.CreatedAt >= from && l.CreatedAt < to)
            .GroupBy(l => l.Action)
            .Select(g => new { Action = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Action, x => x.Count);
    }
}
