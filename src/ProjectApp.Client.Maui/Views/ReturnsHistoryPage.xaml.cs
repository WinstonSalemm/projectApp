using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class ReturnsHistoryPage : ContentPage
{
    public ReturnsHistoryPage(ReturnsHistoryViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

