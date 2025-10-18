using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class ManagerKpiPage : ContentPage
{
    private readonly ManagerKpiViewModel _viewModel;

    public ManagerKpiPage(ManagerKpiViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadManagersKpiAsync();
    }
}
