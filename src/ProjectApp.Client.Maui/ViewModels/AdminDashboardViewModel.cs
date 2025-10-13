using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ProjectApp.Client.Maui.Services;
using System.Threading.Tasks;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class AdminDashboardViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly AuthService _auth;

    public AdminDashboardViewModel(IServiceProvider services, AuthService auth)
    {
        _services = services;
        _auth = auth;
    }

    [RelayCommand]
    private async Task OpenHistoryAsync()
    {
        var page = _services.GetService<ProjectApp.Client.Maui.Views.HistoryTabsPage>();
        if (page != null) await NavigationHelper.PushAsync(page);
    }

    [RelayCommand]
    private async Task OpenSalesHistoryAsync()
    {
        var page = _services.GetService<ProjectApp.Client.Maui.Views.SalesHistoryPage>();
        if (page != null) await NavigationHelper.PushAsync(page);
    }

    [RelayCommand]
    private async Task OpenClientsAsync()
    {
        var page = _services.GetService<ProjectApp.Client.Maui.Views.ClientsListPage>();
        if (page != null) await NavigationHelper.PushAsync(page);
    }

    [RelayCommand]
    private async Task OpenReturnsAsync()
    {
        var page = _services.GetService<ProjectApp.Client.Maui.Views.ReturnsPage>();
        if (page != null) await NavigationHelper.PushAsync(page);
    }

    [RelayCommand]
    private async Task OpenSuppliesAsync()
    {
        var page = _services.GetService<ProjectApp.Client.Maui.Views.SuppliesPage>();
        if (page != null) await NavigationHelper.PushAsync(page);
    }

    [RelayCommand]
    private async Task OpenStocksAsync()
    {
        var page = _services.GetService<ProjectApp.Client.Maui.Views.StocksPage>();
        if (page != null) await NavigationHelper.PushAsync(page);
    }

    [RelayCommand]
    private async Task OpenContractsAsync()
    {
        var page = _services.GetService<ProjectApp.Client.Maui.Views.ContractsListPage>();
        if (page != null) await NavigationHelper.PushAsync(page);
    }

    [RelayCommand]
    private async Task OpenFinanceAsync()
    {
        var page = _services.GetService<ProjectApp.Client.Maui.Views.FinanceDashboardPage>();
        if (page != null) await NavigationHelper.PushAsync(page);
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _auth.LogoutAsync();
        var select = _services.GetRequiredService<ProjectApp.Client.Maui.Views.UserSelectPage>();
        NavigationHelper.SetRoot(new NavigationPage(select));
    }
}

