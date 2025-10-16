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
        _vm.LoadFinanceKpiCommand.Execute(null);
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

    private void OnMonthPeriodTapped(object? sender, EventArgs e)
    {
        // Переключаем стиль кнопок
        PeriodMonth.BackgroundColor = (Color)Application.Current!.Resources["Color.Primary"];
        PeriodYear.BackgroundColor = Colors.Transparent;
        
        // Обновляем период
        _vm.Period = "month";
        _vm.PeriodLabel = "За текущий месяц";
        
        // Перезагружаем данные
        _vm.LoadManagerStatsCommand.Execute(null);
    }

    private void OnYearPeriodTapped(object? sender, EventArgs e)
    {
        // Переключаем стиль кнопок
        PeriodMonth.BackgroundColor = Colors.Transparent;
        PeriodYear.BackgroundColor = (Color)Application.Current!.Resources["Color.Primary"];
        
        // Обновляем период
        _vm.Period = "year";
        _vm.PeriodLabel = "За текущий год";
        
        // Перезагружаем данные
        _vm.LoadManagerStatsCommand.Execute(null);
    }

    private void OnFinanceMonthTapped(object? sender, EventArgs e)
    {
        FinancePeriodMonth.BackgroundColor = (Color)Application.Current!.Resources["Color.Primary"];
        FinancePeriodYear.BackgroundColor = Colors.Transparent;
        _vm.Period = "month";
        _vm.PeriodLabel = "За текущий месяц";
        _vm.LoadFinanceKpiCommand.Execute(null);
    }

    private void OnFinanceYearTapped(object? sender, EventArgs e)
    {
        FinancePeriodMonth.BackgroundColor = Colors.Transparent;
        FinancePeriodYear.BackgroundColor = (Color)Application.Current!.Resources["Color.Primary"];
        _vm.Period = "year";
        _vm.PeriodLabel = "За текущий год";
        _vm.LoadFinanceKpiCommand.Execute(null);
    }

    private void OnFinanceRefreshTapped(object? sender, EventArgs e)
    {
        _vm.LoadFinanceKpiCommand.Execute(null);
    }

    private void OnProductsTabTapped(object? sender, EventArgs e)
    {
        // Переключаем табы
        TabFinance.BackgroundColor = Colors.Transparent;
        TabManagers.BackgroundColor = Colors.Transparent;
        TabProducts.BackgroundColor = (Color)Application.Current!.Resources["Color.Primary"];
        
        FinanceContent.IsVisible = false;
        ManagersContent.IsVisible = false;
        ProductsContent.IsVisible = true;
        
        // Загружаем товары
        _vm.LoadProductCostsCommand.Execute(null);
    }

    private async void OnSaveCostClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is AnalyticsViewModel.ProductCostRow product)
        {
            await _vm.SaveProductCost(product);
        }
    }
}
