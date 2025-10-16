using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.Views;

public partial class SimpleAdminPage : ContentPage
{
    private readonly AuthService _auth;
    
    public SimpleAdminPage(AuthService auth)
    {
        InitializeComponent();
        _auth = auth;
    }

    private async void OnStocksClicked(object? sender, EventArgs e)
    {
        var stocksPage = App.Services.GetRequiredService<StocksPage>();
        await Navigation.PushAsync(stocksPage);
    }

    private async void OnClientsClicked(object? sender, EventArgs e)
    {
        var clientsPage = App.Services.GetRequiredService<ClientsListPage>();
        await Navigation.PushAsync(clientsPage);
    }

    private async void OnAnalyticsClicked(object? sender, EventArgs e)
    {
        var analyticsPage = App.Services.GetRequiredService<AnalyticsPage>();
        await Navigation.PushAsync(analyticsPage);
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        var settingsPage = App.Services.GetRequiredService<SettingsPage>();
        await Navigation.PushAsync(settingsPage);
    }

    private void OnLogoutClicked(object? sender, EventArgs e)
    {
        _auth.Logout();
        var userSelectPage = App.Services.GetRequiredService<UserSelectPage>();
        NavigationHelper.SetRoot(new NavigationPage(userSelectPage));
    }
}
