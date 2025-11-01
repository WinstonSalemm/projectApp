using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProjectApp.Api.Services;

public class PendingConversionRetryHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PendingConversionRetryHostedService> _logger;

    public PendingConversionRetryHostedService(IServiceProvider services, ILogger<PendingConversionRetryHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[PendingConversionRetry] Started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var contractsService = scope.ServiceProvider.GetRequiredService<ContractsService>();

                // Берём пачкой pending отгрузки
                var pendings = await db.ContractDeliveries
                    .Where(d => d.Status == ShipmentStatus.PendingConversion)
                    .OrderBy(d => d.LastRetryAt == null)
                    .ThenBy(d => d.LastRetryAt)
                    .Take(25)
                    .ToListAsync(stoppingToken);

                foreach (var d in pendings)
                {
                    try
                    {
                        await contractsService.RetryPendingDeliveryAsync(d.ContractId, d.Id, userName: "system-job", stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[PendingConversionRetry] Failed for delivery #{DeliveryId}", d.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PendingConversionRetry] Loop error");
            }

            // Пауза между циклами
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch { }
        }
        _logger.LogInformation("[PendingConversionRetry] Stopped");
    }
}
