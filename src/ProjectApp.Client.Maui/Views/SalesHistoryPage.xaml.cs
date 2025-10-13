using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class SalesHistoryPage : ContentPage
{
    public SalesHistoryPage(SalesHistoryViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

