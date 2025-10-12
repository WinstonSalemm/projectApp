using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectApp.Api.Data;

namespace ProjectApp.Api.Services;

public class InventoryCleanupJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InventoryCleanupJob> _logger;

    public InventoryCleanupJob(IServiceScopeFactory scopeFactory, ILogger<InventoryCleanupJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken); // run 4x per day
                await RunOnceAsync(stoppingToken);
            }
            catch (TaskCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InventoryCleanupJob loop error");
                try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); } catch { }
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        // Soft-archive: mark zero batches not already archived
        var zeroBatches = await db.Batches.Where(b => b.ArchivedAt == null && b.Qty == 0).ToListAsync(ct);
        foreach (var b in zeroBatches) b.ArchivedAt = now;

        // Unarchive if qty > 0
        var unarchive = await db.Batches.Where(b => b.ArchivedAt != null && b.Qty > 0).ToListAsync(ct);
        foreach (var b in unarchive) b.ArchivedAt = null;

        if (zeroBatches.Count + unarchive.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("InventoryCleanupJob: archived {Archived} and unarchived {Unarchived} batches", zeroBatches.Count, unarchive.Count);
        }
    }
}
