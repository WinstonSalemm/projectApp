using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class ProductSelectPage : ContentPage
{
    public event EventHandler<ProductSelectViewModel.ProductRow>? ProductPicked;
    private readonly IServiceProvider _services;

    public ProductSelectPage(ProductSelectViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = vm;
        _services = services;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // no-op; we use button click to confirm
    }

    private void OnPickClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is ProductSelectViewModel.ProductRow p)
        {
            ProductPicked?.Invoke(this, p);
        }
    }

    private async void OnCreateClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not ProductSelectViewModel vm) return;
        var page = _services.GetService<ProductCreatePage>();
        if (page == null) return;

        var tcs = new TaskCompletionSource<(int Id, string Sku, string Name, string Category)>();
        void Handler(object? s, (int Id, string Sku, string Name, string Category) p) => tcs.TrySetResult(p);
        page.ProductCreated += Handler;
        await Navigation.PushAsync(page);
        var created = await tcs.Task;
        page.ProductCreated -= Handler;
        await Navigation.PopAsync();

        if (created.Id > 0)
        {
            // Select created category and refresh list
            vm.SelectedCategory = string.IsNullOrWhiteSpace(created.Category) ? vm.SelectedCategory : created.Category;
            await vm.LoadCategoriesAsync();
            await vm.SearchAsync();
        }
    }
}

