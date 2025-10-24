using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class CostingPage : ContentPage
{
    public CostingPage(CostingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
