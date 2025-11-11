using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectApp.Client.Maui.Views;

public partial class ReservationDetailsPage : ContentPage
{
    private readonly IServiceProvider _services;

    public ReservationDetailsPage(ReservationDetailsViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = vm;
        _services = services;
    }

    private async void OnAddProductClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not ReservationDetailsViewModel vm) return;
        var page = _services.GetRequiredService<ProductSelectPage>();
        page.IsPicker = true;

        var tcs = new TaskCompletionSource<ProjectApp.Client.Maui.ViewModels.ProductSelectViewModel.ProductRow>();
        void PickHandler(object? s, ProjectApp.Client.Maui.ViewModels.ProductSelectViewModel.ProductRow p) => tcs.TrySetResult(p);
        page.ProductPicked += PickHandler;
        await Navigation.PushAsync(page);
        var product = await tcs.Task;
        page.ProductPicked -= PickHandler;
        await Navigation.PopAsync();

        if (product != null)
        {
            await vm.AddProductAsync(product.Id, 1);
        }
    }
}
