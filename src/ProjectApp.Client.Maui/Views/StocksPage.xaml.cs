using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class StocksPage : ContentPage
{
    public StocksPage(StocksViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private async void OnDefectivesClicked(object sender, EventArgs e)
    {
        try
        {
            var page = App.Services.GetRequiredService<DefectivesPage>();
            await Navigation.PushAsync(page);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", $"Не удалось открыть страницу: {ex.Message}", "OK");
        }
    }

    private async void OnRefillsClicked(object sender, EventArgs e)
    {
        try
        {
            var page = App.Services.GetRequiredService<RefillsPage>();
            await Navigation.PushAsync(page);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", $"Не удалось открыть страницу: {ex.Message}", "OK");
        }
    }
}

