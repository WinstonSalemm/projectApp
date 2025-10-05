using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.Messages;
using ProjectApp.Client.Maui.Models;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ClientPickerViewModel : ObservableObject
{
    private readonly ApiClientsService _clients;
    private readonly AuthService _auth;

    public ObservableCollection<ClientListItem> Items { get; } = new();

    [ObservableProperty]
    private string query = string.Empty;

    [ObservableProperty]
    private ClientType? selectedType = null;

    [ObservableProperty]
    private bool showOnlyMine = true;

    [ObservableProperty]
    private bool isLoading;

    public ClientPickerViewModel(ApiClientsService clients, AuthService auth)
    {
        _clients = clients;
        _auth = auth;
        showOnlyMine = !string.Equals(_auth.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync(int page = 1)
    {
        if (IsLoading) return;
        try
        {
            IsLoading = true;
            string? owner = null;
            var isAdmin = string.Equals(_auth.Role, "Admin", StringComparison.OrdinalIgnoreCase);
            if (!isAdmin || ShowOnlyMine) owner = _auth.UserName;
            var (items, total) = await _clients.ListAsync(string.IsNullOrWhiteSpace(Query) ? null : Query, SelectedType, owner, page, 50);
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                Items.Clear();
                foreach (var c in items) Items.Add(c);
            });
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task PickAsync(ClientListItem? item)
    {
        if (item == null) return;
        WeakReferenceMessenger.Default.Send(new ClientPickedMessage(item.Id, item.Name));
        try { await Application.Current!.MainPage!.Navigation.PopAsync(); } catch { }
    }
}
