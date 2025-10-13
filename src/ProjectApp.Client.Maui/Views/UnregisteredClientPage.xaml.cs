using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class UnregisteredClientPage : TabbedPage
{
    public UnregisteredClientPage(UnregisteredClientViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

