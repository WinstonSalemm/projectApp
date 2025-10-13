using CommunityToolkit.Mvvm.ComponentModel;

namespace ProjectApp.Client.Maui.Models;

public partial class CartItemModel : ObservableObject
{
    [ObservableProperty]
    private int productId;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private decimal unitPrice;

    [ObservableProperty]
    private double qty = 1d;

    public decimal Subtotal => UnitPrice * (decimal)Qty;

    partial void OnUnitPriceChanged(decimal value)
    {
        OnPropertyChanged(nameof(Subtotal));
    }

    partial void OnQtyChanged(double value)
    {
        OnPropertyChanged(nameof(Subtotal));
    }
}

