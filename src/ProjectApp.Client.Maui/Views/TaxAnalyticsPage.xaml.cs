using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class TaxAnalyticsPage : ContentPage
{
    private readonly TaxAnalyticsViewModel _viewModel;

    public TaxAnalyticsPage(TaxAnalyticsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadMonthlyReportAsync();
    }
}
