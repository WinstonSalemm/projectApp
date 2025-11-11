using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;
using System.Collections.ObjectModel;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ReservationsListViewModel : ObservableObject
{
    private readonly IReservationsService _reservations;

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string? statusFilter;
    [ObservableProperty] private string? search;
    [ObservableProperty] private DateTime? dateFrom;
    [ObservableProperty] private DateTime? dateTo;

    public enum DateFilterMode { Today, Week, Month, Custom }
    [ObservableProperty] private DateFilterMode dateFilter = DateFilterMode.Month;

    public ObservableCollection<ReservationListItem> Items { get; } = new();

    public ReservationsListViewModel(IReservationsService reservations)
    {
        _reservations = reservations;
        // Default to current month
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        DateFrom = startOfMonth;
        DateTo = startOfMonth.AddMonths(1);
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        try
        {
            IsLoading = true;
            Items.Clear();
            var (from, to) = GetEffectiveRange();
            var list = await _reservations.GetReservationsAsync(status: StatusFilter, clientId: null, mine: null, dateFrom: from, dateTo: to);
            // simple search by client name or id
            if (!string.IsNullOrWhiteSpace(Search))
            {
                var q = Search.Trim().ToLowerInvariant();
                list = list.Where(x => (x.ClientName ?? "").ToLower().Contains(q) || x.Id.ToString().Contains(q)).ToList();
            }
            foreach (var r in list)
                Items.Add(r);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private (DateTime?, DateTime?) GetEffectiveRange()
    {
        var now = DateTime.UtcNow;
        return DateFilter switch
        {
            DateFilterMode.Today => (now.Date, now.Date.AddDays(1)),
            DateFilterMode.Week => (now.Date.AddDays(-6), now.Date.AddDays(1)), // последние 7 дней
            DateFilterMode.Month => (new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1)),
            DateFilterMode.Custom => (DateFrom, DateTo),
            _ => (DateFrom, DateTo)
        };
    }
}
