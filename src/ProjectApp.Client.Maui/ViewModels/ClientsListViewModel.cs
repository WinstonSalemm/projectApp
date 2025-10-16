using System.Collections.ObjectModel;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.Models;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ClientsListViewModel : ObservableObject
{
    private readonly ApiClientsService _clients;
    private readonly AuthService _auth;
    private readonly IServiceProvider _services;

    public ObservableCollection<ClientListItem> Items { get; } = new();
    public ObservableCollection<string> Managers { get; } = new();

    [ObservableProperty]
    private string query = string.Empty;

    [ObservableProperty]
    private ClientType? selectedType = null; // null = all

    [ObservableProperty]
    private string? selectedManager = null;

    [ObservableProperty]
    private bool showOnlyMine = true;

    [ObservableProperty]
    private bool isLoading;

    public ClientsListViewModel(ApiClientsService clients, AuthService auth, IServiceProvider services)
    {
        _clients = clients;
        _auth = auth;
        _services = services;
        showOnlyMine = !string.Equals(_auth.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        _ = LoadManagersAsync();
        _ = LoadAsync();
    }

    private async Task LoadManagersAsync()
    {
        try
        {
            // Загружаем список пользователей из API
            var client = new System.Net.Http.HttpClient 
            { 
                BaseAddress = new Uri("https://tranquil-upliftment-production.up.railway.app") 
            };
            
            var response = await client.GetAsync("/api/users");
            if (response.IsSuccessStatusCode)
            {
                var userList = await response.Content.ReadFromJsonAsync<List<UserItem>>();
                var users = userList?.Select(u => u.UserName ?? u.DisplayName).Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? new List<string>();
                
                await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Managers.Clear();
                    Managers.Add("Все менеджеры");
                    foreach (var u in users)
                    {
                        if (!string.IsNullOrWhiteSpace(u))
                            Managers.Add(u);
                    }
                });
            }
        }
        catch { }
    }
    
    private class UserItem
    {
        public int Id { get; set; }
        public string? UserName { get; set; }
        public string? DisplayName { get; set; }
        public string? Role { get; set; }
        public bool IsActive { get; set; }
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
            
            // Фильтр по менеджеру (если выбран конкретный)
            if (!string.IsNullOrWhiteSpace(SelectedManager) && SelectedManager != "Все менеджеры")
            {
                owner = SelectedManager;
            }
            else if (!isAdmin || ShowOnlyMine)
            {
                owner = _auth.UserName;
            }
            
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
    private async Task OpenCreateAsync()
    {
        var page = _services.GetService<ProjectApp.Client.Maui.Views.ClientCreatePage>();
        if (page != null) await NavigationHelper.PushAsync(page);
    }

    [RelayCommand]
    private async Task OpenDetailAsync(ClientListItem? item)
    {
        if (item == null) return;
        var page = _services.GetService<ProjectApp.Client.Maui.Views.ClientDetailPage>();
        if (page is null) return;
        if (page.BindingContext is ProjectApp.Client.Maui.ViewModels.ClientDetailViewModel vm)
        {
            await vm.LoadAsync(item.Id);
        }
        await NavigationHelper.PushAsync(page);
    }

    [RelayCommand]
    private async Task OpenUnregisteredAsync()
    {
        var page = _services.GetService<ProjectApp.Client.Maui.Views.UnregisteredClientPage>();
        if (page is null) return;
        if (page.BindingContext is ProjectApp.Client.Maui.ViewModels.UnregisteredClientViewModel vm)
        {
            await vm.LoadAsync();
        }
        await NavigationHelper.PushAsync(page);
    }
}


