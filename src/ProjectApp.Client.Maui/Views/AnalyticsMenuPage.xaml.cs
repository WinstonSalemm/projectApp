namespace ProjectApp.Client.Maui.Views;

public partial class AnalyticsMenuPage : ContentPage
{
    public AnalyticsMenuPage()
    {
        InitializeComponent();
    }

    private async void OnFinancesTapped(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new FinanceAnalyticsPage());
    }

    private async void OnManagersTapped(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new ManagerAnalyticsPage());
    }

    private async void OnProductCostsTapped(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new ProductCostsPage());
    }
}
