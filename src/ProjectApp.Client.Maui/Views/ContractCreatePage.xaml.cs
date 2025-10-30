using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class ContractCreatePage : ContentPage
{
    private readonly IServiceProvider _services;

    public ContractCreatePage(ContractCreateViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = vm;
        _services = services;
    }

    private async void OnPickProductClicked(object? sender, EventArgs e)
    {
        var vm = BindingContext as ContractCreateViewModel;
        if (vm == null) return;

        var page = _services.GetService<ProductSelectPage>();
        if (page == null) return;
        page.IsPicker = true;

        var tcs = new TaskCompletionSource<ProductSelectViewModel.ProductRow?>();
        void Handler(object? s, ProductSelectViewModel.ProductRow p)
        {
            tcs.TrySetResult(p);
        }
        page.ProductPicked += Handler;
        await Navigation.PushAsync(page);
        var picked = await tcs.Task;
        page.ProductPicked -= Handler;
        await Navigation.PopAsync();

        if (picked != null)
        {
            vm.NewProductId = picked.Id;
            vm.NewName = string.IsNullOrWhiteSpace(picked.Name) ? picked.Sku : picked.Name;
            vm.NewUnit = string.IsNullOrWhiteSpace(picked.Unit) ? "шт" : picked.Unit;
            // Цена всегда вводится вручную
            vm.NewUnitPrice = 0m;
            // Stocks for current product
            vm.SelectedNd40Qty = picked.Nd40Qty;
            vm.SelectedIm40Qty = picked.Im40Qty;
            vm.SelectedTotalQty = picked.TotalQty;
        }
    }
}

