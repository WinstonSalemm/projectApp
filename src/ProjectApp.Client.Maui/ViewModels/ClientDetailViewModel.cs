using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.Messages;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ClientDetailViewModel : ObservableObject
{
    private readonly ApiClientsService _clients;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private int clientId;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string? phone;

    [ObservableProperty]
    private string? inn;

    [ObservableProperty]
    private ProjectApp.Client.Maui.Models.ClientType type;

    [ObservableProperty]
    private string? ownerUserName;

    [ObservableProperty]
    private DateTime createdAt;

    public ObservableCollection<ApiClientsService.SaleBriefDto> Sales { get; } = new();
    public ObservableCollection<ApiClientsService.ReturnBriefDto> Returns { get; } = new();
    public ObservableCollection<ApiClientsService.DebtListItem> Debts { get; } = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private DateTime? dateFrom = DateTime.UtcNow.Date.AddMonths(-1);

    [ObservableProperty]
    private DateTime? dateTo = DateTime.UtcNow.Date.AddDays(1);

    public ClientDetailViewModel(ApiClientsService clients, IServiceProvider services)
    {
        _clients = clients;
        _services = services;

        WeakReferenceMessenger.Default.Register<ClientUpdatedMessage>(this, (r, m) =>
        {
            if (m.ClientId == ClientId)
            {
                Name = m.Name;
                Phone = m.Phone;
                Inn = m.Inn;
                Type = m.Type;
            }
        });
    }

    public async Task LoadAsync(int id)
    {
        ClientId = id;
        var c = await _clients.GetAsync(id);
        if (c != null)
        {
            Name = c.Name;
            Phone = c.Phone;
            Inn = c.Inn;
            Type = c.Type;
            OwnerUserName = c.OwnerUserName;
            CreatedAt = c.CreatedAt;
        }
        await RefreshAllAsync();
    }

    [RelayCommand]
    private async Task RefreshAllAsync()
    {
        await Task.WhenAll(RefreshSalesAsync(), RefreshReturnsAsync(), RefreshDebtsAsync());
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        var page = _services.GetService<ProjectApp.Client.Maui.Views.ClientEditPage>();
        if (page is null) return;
        if (page.BindingContext is ProjectApp.Client.Maui.ViewModels.ClientEditViewModel vm)
        {
            await vm.LoadAsync(ClientId);
        }
        await Application.Current!.MainPage!.Navigation.PushAsync(page);
    }

    [RelayCommand]
    private async Task RefreshSalesAsync()
    {
        if (IsLoading) return;
        try
        {
            IsLoading = true;
            var list = await _clients.GetSalesAsync(ClientId, DateFrom, DateTo);
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                Sales.Clear();
                foreach (var s in list) Sales.Add(s);
            });
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task RefreshReturnsAsync()
    {
        var list = await _clients.GetReturnsAsync(ClientId, DateFrom, DateTo);
        await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
        {
            Returns.Clear();
            foreach (var r in list) Returns.Add(r);
        });
    }

    [RelayCommand]
    private async Task RefreshDebtsAsync()
    {
        var list = await _clients.GetDebtsAsync(ClientId);
        await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
        {
            Debts.Clear();
            foreach (var d in list) Debts.Add(d);
        });
    }
}
