using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class SuppliesPage : ContentPage
{
    private readonly IServiceProvider _services;

    public SuppliesPage(SuppliesViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = vm;
        _services = services;
    }

    private async void OnPickProductForSupplyClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not SuppliesViewModel vm) return;
        var page = _services.GetService<ProductSelectPage>();
        if (page == null) return;

        var tcs = new TaskCompletionSource<ProductSelectViewModel.ProductRow?>();
        void Handler(object? s, ProductSelectViewModel.ProductRow p) => tcs.TrySetResult(p);
        page.ProductPicked += Handler;
        await Navigation.PushAsync(page);
        var picked = await tcs.Task;
        page.ProductPicked -= Handler;
        await Navigation.PopAsync();

        if (picked != null)
        {
            vm.NewProductId = picked.Id;
            vm.SelectedNd40Qty = picked.Nd40Qty;
            vm.SelectedIm40Qty = picked.Im40Qty;
            vm.SelectedTotalQty = picked.TotalQty;
        }
    }

    private async void OnPickProductForTransferClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not SuppliesViewModel vm) return;
        var page = _services.GetService<ProductSelectPage>();
        if (page == null) return;

        var tcs = new TaskCompletionSource<ProductSelectViewModel.ProductRow?>();
        void Handler(object? s, ProductSelectViewModel.ProductRow p) => tcs.TrySetResult(p);
        page.ProductPicked += Handler;
        await Navigation.PushAsync(page);
        var picked = await tcs.Task;
        page.ProductPicked -= Handler;
        await Navigation.PopAsync();

        if (picked != null)
        {
            vm.TransferProductId = picked.Id;
            vm.TransferNd40Qty = picked.Nd40Qty;
            vm.TransferIm40Qty = picked.Im40Qty;
            vm.TransferTotalQty = picked.TotalQty;
        }
    }
}
