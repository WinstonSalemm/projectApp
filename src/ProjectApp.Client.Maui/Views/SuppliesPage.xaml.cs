using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class SuppliesPage : ContentPage
{
    public SuppliesPage(SuppliesViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
