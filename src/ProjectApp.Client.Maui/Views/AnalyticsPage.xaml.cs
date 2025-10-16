using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class AnalyticsPage : ContentPage
{
    private readonly AnalyticsViewModel _vm;

    public AnalyticsPage(AnalyticsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadManagerStatsCommand.Execute(null);
    }

    private void OnFinanceTabTapped(object? sender, EventArgs e)
    {
        // Переключаем табы
        TabFinance.BackgroundColor = (Color)Application.Current!.Resources["Color.Primary"];
        TabManagers.BackgroundColor = Colors.Transparent;
        
        FinanceContent.IsVisible = true;
        ManagersContent.IsVisible = false;
    }

    private void OnManagersTabTapped(object? sender, EventArgs e)
    {
        // Переключаем табы
        TabFinance.BackgroundColor = Colors.Transparent;
        TabManagers.BackgroundColor = (Color)Application.Current!.Resources["Color.Primary"];
        
        FinanceContent.IsVisible = false;
        ManagersContent.IsVisible = true;
        
        // Загружаем статистику
        _vm.LoadManagerStatsCommand.Execute(null);
    }
}
