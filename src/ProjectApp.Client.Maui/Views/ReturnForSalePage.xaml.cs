using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class ReturnForSalePage : ContentPage
{
    public ReturnForSalePage(ReturnForSaleViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

