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

    private async void OnSuppliesClicked(object? sender, EventArgs e)
    {
        var suppliesPage = App.Services.GetRequiredService<SuppliesPage>();
        await Navigation.PushAsync(suppliesPage);
    }

    private async void OnClientsClicked(object? sender, EventArgs e)
    {
        var clientsPage = App.Services.GetRequiredService<ClientsListPage>();
        await Navigation.PushAsync(clientsPage);
    }

    private async void OnHistoryClicked(object? sender, EventArgs e)
    {
        var historyPage = App.Services.GetRequiredService<SalesHistoryPage>();
        await Navigation.PushAsync(historyPage);
    }

    private async void OnFinancesClicked(object? sender, EventArgs e)
    {
        var financesPage = App.Services.GetRequiredService<FinancesMenuPage>();
        await Navigation.PushAsync(financesPage);
    }

    private async void OnAnalyticsClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new AnalyticsMenuPage());
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
