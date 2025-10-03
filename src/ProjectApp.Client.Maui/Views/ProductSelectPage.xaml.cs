using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;
using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.Views;

public partial class ProductSelectPage : ContentPage
{
    public event EventHandler<ProductModel>? ProductPicked;

    public ProductSelectPage(ProductSelectViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // no-op; we use button click to confirm
    }

    private void OnPickClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is ProductModel p)
        {
            ProductPicked?.Invoke(this, p);
        }
    }
}
