using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Services;

public class ReservationsCleanupService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<ReservationsCleanupService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

    public ReservationsCleanupService(IServiceProvider sp, ILogger<ReservationsCleanupService> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _sp.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTime.UtcNow;
                var expired = await db.Reservations
                    .Where(r => r.Status == ReservationStatus.Active && r.ReservedUntil < now)
                    .ToListAsync(stoppingToken);
                if (expired.Count > 0)
                {
                    foreach (var r in expired)
                    {
                        r.Status = ReservationStatus.Expired;
                        db.ReservationLogs.Add(new ReservationLog
                        {
                            ReservationId = r.Id,
                            Action = "Expired",
                            UserName = "system",
                            CreatedAt = now,
                            Details = null
                        });
                    }
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Expired {Count} reservations", expired.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reservations cleanup error");
            }

            try { await Task.Delay(_interval, stoppingToken); } catch { }
        }
    }
}
