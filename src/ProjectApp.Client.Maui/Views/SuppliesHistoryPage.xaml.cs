using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class SuppliesHistoryPage : ContentPage
{
    public SuppliesHistoryPage(SuppliesHistoryViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

