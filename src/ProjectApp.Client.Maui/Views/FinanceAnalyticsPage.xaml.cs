using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class FinanceAnalyticsPage : ContentPage
{
    private readonly AnalyticsViewModel _vm;

    public FinanceAnalyticsPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<AnalyticsViewModel>();
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadFinanceKpiCommand.Execute(null);
    }

    private void OnMonthTapped(object? sender, EventArgs e)
    {
        PeriodMonth.BackgroundColor = (Color)Application.Current!.Resources["Color.Primary"];
        PeriodYear.BackgroundColor = Colors.Transparent;
        _vm.Period = "month";
        _vm.PeriodLabel = "За текущий месяц";
        _vm.LoadFinanceKpiCommand.Execute(null);
    }

    private void OnYearTapped(object? sender, EventArgs e)
    {
        PeriodMonth.BackgroundColor = Colors.Transparent;
        PeriodYear.BackgroundColor = (Color)Application.Current!.Resources["Color.Primary"];
        _vm.Period = "year";
        _vm.PeriodLabel = "За текущий год";
        _vm.LoadFinanceKpiCommand.Execute(null);
    }

    private void OnRefreshTapped(object? sender, EventArgs e)
    {
        _vm.LoadFinanceKpiCommand.Execute(null);
    }
}
