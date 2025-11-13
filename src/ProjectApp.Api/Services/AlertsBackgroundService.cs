namespace ProjectApp.Api.Services;

/// <summary>
/// Фоновый сервис для периодической проверки алертов и отправки отчетов
/// </summary>
public class AlertsBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AlertsBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1); // Проверка каждый час
    private DateTime _lastDailyReport = DateTime.MinValue;
    private DateTime _lastWeeklyReport = DateTime.MinValue;
    private DateTime _lastReservationReminder = DateTime.MinValue;

    public AlertsBackgroundService(
        IServiceProvider services,
        ILogger<AlertsBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AlertsBackgroundService запущен");

        // Ждем 1 минуту после старта приложения
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunChecksAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в AlertsBackgroundService");
            }

            // Ждем до следующей проверки
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("AlertsBackgroundService остановлен");
    }

    private async Task RunChecksAsync()
    {
        using var scope = _services.CreateScope();
        var alertsService = scope.ServiceProvider.GetRequiredService<AlertsService>();
        var reportsService = scope.ServiceProvider.GetRequiredService<AutoReportsService>();

        var now = DateTime.UtcNow;
        var nowLocal = DateTime.Now;

        // 1. Проверяем алерты (каждый раз при запуске)
        await alertsService.CheckCriticalStocksAsync();
        await alertsService.CheckOverdueDebtsAsync();
        await alertsService.CheckLongPendingReservationsAsync();

        // Напоминания по броням раз в 2 дня
        // TODO: Реализовать CheckReservationRemindersAsync в AlertsService
        // if ((now - _lastReservationReminder).TotalHours >= 47)
        // {
        //     await alertsService.CheckReservationRemindersAsync();
        //     _lastReservationReminder = now;
        // }

        // 2. Ежедневный отчет в 21:00
        if (nowLocal.Hour == 21 && 
            (now - _lastDailyReport).TotalHours >= 23)
        {
            await reportsService.SendDailyReportAsync();
            _lastDailyReport = now;
            _logger.LogInformation("Ежедневный отчет отправлен");
        }

        // 3. Еженедельный отчет по понедельникам в 9:00
        if (nowLocal.DayOfWeek == DayOfWeek.Monday && 
            nowLocal.Hour == 9 && 
            (now - _lastWeeklyReport).TotalDays >= 6)
        {
            await reportsService.SendWeeklyReportAsync();
            _lastWeeklyReport = now;
            _logger.LogInformation("Еженедельный отчет отправлен");
        }
    }
}
