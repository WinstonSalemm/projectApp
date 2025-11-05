using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Dispatching;

namespace ProjectApp.Client.Maui.Services;

public class LocalReservationNotifier : IAsyncDisposable
{
    private readonly IReservationsService _reservations;
    private readonly IDispatcher _dispatcher;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    private const string LastGlobalRunKey = "ReservationNotifier.LastRunUtc";
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(6); // опрос каждые 6 часов
    private static readonly TimeSpan ReminderCadence = TimeSpan.FromDays(2); // показывать раз в 2 дня

    public LocalReservationNotifier(IReservationsService reservations)
    {
        _reservations = reservations;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.GetForCurrentThread()!;
    }

    public void Start()
    {
        if (_loopTask != null && !_loopTask.IsCompleted) return; // уже запущен
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => LoopAsync(_cts.Token));
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _cts?.Cancel();
            if (_loopTask != null) await _loopTask.ConfigureAwait(false);
        }
        catch { }
        finally
        {
            _cts?.Dispose();
        }
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var lastRunTicks = Preferences.Get(LastGlobalRunKey, 0L);
                DateTime? since = lastRunTicks > 0 ? new DateTime(lastRunTicks, DateTimeKind.Utc) : null;
                await CheckAndNotifyAsync(since, ct).ConfigureAwait(false);
                Preferences.Set(LastGlobalRunKey, DateTime.UtcNow.Ticks);
            }
            catch { }

            try { await Task.Delay(PollInterval, ct); } catch { }
        }
    }

    private async Task CheckAndNotifyAsync(DateTime? sinceUtc, CancellationToken ct)
    {
        var alerts = await _reservations.GetAlertsAsync(sinceUtc, ct).ConfigureAwait(false);
        foreach (var r in alerts)
        {
            // Не спамим чаще, чем раз в 48 часов для каждой брони
            var key = $"ReservationNotify:{r.Id}";
            var lastTicks = Preferences.Get(key, 0L);
            var canNotify = true;
            if (lastTicks > 0)
            {
                var last = new DateTime(lastTicks, DateTimeKind.Utc);
                canNotify = (DateTime.UtcNow - last) >= ReminderCadence;
            }
            if (!canNotify) continue;

            var header = $"Бронь #{r.Id} — {(r.Paid ? "Оплачено" : "Не оплачено")}";
            var client = string.IsNullOrWhiteSpace(r.ClientName) ? "Клиент: —" : $"{r.ClientName}";
            var phone = string.IsNullOrWhiteSpace(r.ClientPhone) ? "" : $" | {r.ClientPhone}";
            var until = r.ReservedUntil.HasValue ? $"до {r.ReservedUntil:dd.MM.yyyy HH:mm}" : "срок не задан";
            var text = $"{client}{phone}\nСтатус: {r.Status} | {until}";

            await ShowToastAsync($"{header}\n{text}");
            Preferences.Set(key, DateTime.UtcNow.Ticks);
        }
    }

    private Task ShowToastAsync(string message)
    {
        return _dispatcher.DispatchAsync(async () =>
        {
            var toast = Toast.Make(message, ToastDuration.Long, 14);
            await toast.Show();
        });
    }
}
