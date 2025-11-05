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

    public ObservableCollection<ReservationListItem> Items { get; } = new();

    public ReservationsListViewModel(IReservationsService reservations)
    {
        _reservations = reservations;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        try
        {
            IsLoading = true;
            Items.Clear();
            var list = await _reservations.GetReservationsAsync(status: StatusFilter, clientId: null, mine: null);
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
}
