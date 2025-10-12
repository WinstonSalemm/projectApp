using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectApp.Api.Data;
using ProjectApp.Api.Modules.Finance.Models;

namespace ProjectApp.Api.Modules.Finance;

public class FinanceSnapshotJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FinanceSnapshotJob> _logger;
    private readonly FinanceSettings _settings;

    public FinanceSnapshotJob(IServiceScopeFactory scopeFactory, IOptions<FinanceSettings> settings, ILogger<FinanceSnapshotJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nowUtc = DateTime.UtcNow;
                var offset = TimeSpan.FromMinutes(_settings.TimeZoneOffsetMinutes);
                var nowLocal = nowUtc + offset;

                var targetLocal = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 23, 30, 0, DateTimeKind.Unspecified);
                var targetUtc = targetLocal - offset;
                if (targetUtc <= nowUtc)
                {
                    targetUtc = targetUtc.AddDays(1);
                }
                var delay = targetUtc - nowUtc;
                _logger.LogInformation("FinanceSnapshotJob: sleeping for {Delay} until {TargetUtc}", delay, targetUtc);
                await Task.Delay(delay, stoppingToken);

                await RunOnceAsync(stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinanceSnapshotJob: error in loop");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<FinanceService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var todayUtc = DateTime.UtcNow.Date;
        var snapshot = await svc.BuildSnapshotAsync(todayUtc, ct);

        var existing = await db.FinanceSnapshots.FirstOrDefaultAsync(x => x.Date == snapshot.Date, ct);
        if (existing is null)
        {
            db.FinanceSnapshots.Add(snapshot);
        }
        else
        {
            existing.Revenue = snapshot.Revenue;
            existing.Cogs = snapshot.Cogs;
            existing.GrossProfit = snapshot.GrossProfit;
            existing.Expenses = snapshot.Expenses;
            existing.TaxesPaid = snapshot.TaxesPaid;
            existing.NetProfit = snapshot.NetProfit;
            existing.SalesCount = snapshot.SalesCount;
            existing.UniqueClients = snapshot.UniqueClients;
            existing.AverageInventory = snapshot.AverageInventory;
        }
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("FinanceSnapshotJob: snapshot saved for {Date}", snapshot.Date);
    }
}
