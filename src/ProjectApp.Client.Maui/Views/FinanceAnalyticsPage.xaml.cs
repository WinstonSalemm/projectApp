using Microsoft.Maui.Controls;
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
        try
        {
            _vm.LoadFinanceKpiCommand.Execute(null);
        }
        catch { }
    }
}
