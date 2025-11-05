using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class SaleEditPage : ContentPage
{
    public SaleEditPage(SaleEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
