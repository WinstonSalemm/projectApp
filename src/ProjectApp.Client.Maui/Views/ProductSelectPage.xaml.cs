using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class ProductSelectPage : ContentPage
{
    public event EventHandler<ProductSelectViewModel.ProductRow>? ProductPicked;

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
        if ((sender as Button)?.CommandParameter is ProductSelectViewModel.ProductRow p)
        {
            ProductPicked?.Invoke(this, p);
        }
    }
}
