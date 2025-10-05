using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class UnregisteredClientViewModel : ObservableObject
{
    private readonly ApiClientsService _clients;

    public ObservableCollection<ApiClientsService.SaleBriefDto> Sales { get; } = new();
    public ObservableCollection<ApiClientsService.ReturnBriefDto> Returns { get; } = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private DateTime? dateFrom = DateTime.UtcNow.Date.AddMonths(-1);

    [ObservableProperty]
    private DateTime? dateTo = DateTime.UtcNow.Date.AddDays(1);

    public UnregisteredClientViewModel(ApiClientsService clients)
    {
        _clients = clients;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RefreshAllAsync();
    }

    [RelayCommand]
    public async Task RefreshAllAsync()
    {
        await Task.WhenAll(RefreshSalesAsync(), RefreshReturnsAsync());
    }

    [RelayCommand]
    public async Task RefreshSalesAsync()
    {
        if (IsLoading) return;
        try
        {
            IsLoading = true;
            var list = await _clients.GetUnregisteredSalesAsync(DateFrom, DateTo);
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                Sales.Clear();
                foreach (var s in list) Sales.Add(s);
            });
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task RefreshReturnsAsync()
    {
        var list = await _clients.GetUnregisteredReturnsAsync(DateFrom, DateTo);
        await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
        {
            Returns.Clear();
            foreach (var r in list) Returns.Add(r);
        });
    }
}
