using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class SuppliesPage : ContentPage
{
    public SuppliesPage(SuppliesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is SuppliesViewModel vm)
        {
            await vm.LoadSuppliesCommand.ExecuteAsync(null);
        }
    }
}
