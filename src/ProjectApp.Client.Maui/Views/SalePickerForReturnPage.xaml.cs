using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class SalePickerForReturnPage : ContentPage
{
    private readonly SalePickerForReturnViewModel _vm;

    public SalePickerForReturnPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<SalePickerForReturnViewModel>();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadSalesCommand.ExecuteAsync(null);
    }
}
