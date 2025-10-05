using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class SaleStartPage : ContentPage
{
    private readonly IServiceProvider _services;

    public SaleStartPage(SaleStartViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = vm;
        _services = services;
    }

    private async void OnCategoryClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not SaleStartViewModel vm) return;
        string? category = null;
        if (sender is Button btn)
            category = btn.BindingContext as string;
        var qs = _services.GetRequiredService<QuickSalePage>();
        if (qs.BindingContext is QuickSaleViewModel qvm)
        {
            qvm.SelectedPaymentType = vm.SelectedPaymentType;
            qvm.SetPresetCategory(category);
        }
        await Navigation.PushAsync(qs);
    }
}
