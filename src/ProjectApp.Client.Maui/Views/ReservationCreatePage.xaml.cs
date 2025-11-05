using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class ReservationCreatePage : ContentPage
{
    private readonly IServiceProvider _services;

    public ReservationCreatePage(ReservationCreateViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = vm;
        _services = services;
    }

    private async void OnSelectClientClicked(object sender, EventArgs e)
    {
        if (BindingContext is not ReservationCreateViewModel vm) return;
        var clientPage = _services.GetRequiredService<ClientSelectPage>();
        var tcs = new TaskCompletionSource<(int? Id, string Name)>();
        void Handler(object? s, (int? Id, string Name) client) => tcs.TrySetResult(client);
        clientPage.ClientSelected += Handler;
        await Navigation.PushAsync(clientPage);
        var selected = await tcs.Task;
        clientPage.ClientSelected -= Handler;
        await Navigation.PopAsync();
        vm.SetClient(selected.Id, selected.Name);
    }

    private async void OnAddProductClicked(object sender, EventArgs e)
    {
        if (BindingContext is not ReservationCreateViewModel vm) return;
        var page = _services.GetRequiredService<ProductSelectPage>();
        page.IsPicker = true;
        var tcs = new TaskCompletionSource<ProductSelectViewModel.ProductRow>();
        void PickHandler(object? s, ProductSelectViewModel.ProductRow p) => tcs.TrySetResult(p);
        page.ProductPicked += PickHandler;
        await Navigation.PushAsync(page);
        var product = await tcs.Task;
        page.ProductPicked -= PickHandler;
        await Navigation.PopAsync();
        if (product != null)
        {
            vm.AddProduct(product.Id, product.Sku, product.Name, product.Price);
        }
    }

    private void OnRemoveClicked(object sender, EventArgs e)
    {
        if (BindingContext is not ReservationCreateViewModel vm) return;
        if (sender is not Button btn) return;
        if (btn.CommandParameter is ReservationCreateViewModel.ItemRow row)
        {
            vm.RemoveItem(row);
        }
    }
}
