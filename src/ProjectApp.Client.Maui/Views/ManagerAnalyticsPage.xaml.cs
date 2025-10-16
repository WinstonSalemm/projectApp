using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class ManagerAnalyticsPage : ContentPage
{
    private readonly AnalyticsViewModel _vm;

    public ManagerAnalyticsPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<AnalyticsViewModel>();
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadManagerStatsCommand.Execute(null);
    }

    private void OnMonthTapped(object? sender, EventArgs e)
    {
        PeriodMonth.BackgroundColor = (Color)Application.Current!.Resources["Color.Primary"];
        PeriodYear.BackgroundColor = Colors.Transparent;
        _vm.Period = "month";
        _vm.PeriodLabel = "За текущий месяц";
        _vm.LoadManagerStatsCommand.Execute(null);
    }

    private void OnYearTapped(object? sender, EventArgs e)
    {
        PeriodMonth.BackgroundColor = Colors.Transparent;
        PeriodYear.BackgroundColor = (Color)Application.Current!.Resources["Color.Primary"];
        _vm.Period = "year";
        _vm.PeriodLabel = "За текущий год";
        _vm.LoadManagerStatsCommand.Execute(null);
    }
}
