using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class ContractsPage : ContentPage
{
    public ContractsPage(ContractsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is ContractsViewModel vm)
        {
            vm.LoadContractsCommand.Execute(null);
        }
    }
}
