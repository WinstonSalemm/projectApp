using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class DebtCreatePage : ContentPage
{
    public DebtCreatePage(DebtCreateViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
