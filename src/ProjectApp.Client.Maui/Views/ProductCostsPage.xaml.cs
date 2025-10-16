using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class ProductCostsPage : ContentPage
{
    private readonly AnalyticsViewModel _vm;

    public ProductCostsPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<AnalyticsViewModel>();
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadProductCostsCommand.Execute(null);
    }

    private async void OnSaveCostClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is AnalyticsViewModel.ProductCostRow product)
        {
            await _vm.SaveProductCost(product);
        }
    }
}
