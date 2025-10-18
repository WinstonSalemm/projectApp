using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class CommissionAgentsPage : ContentPage
{
    private readonly CommissionAgentsViewModel _viewModel;

    public CommissionAgentsPage(CommissionAgentsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAgentsAsync();
    }
}
