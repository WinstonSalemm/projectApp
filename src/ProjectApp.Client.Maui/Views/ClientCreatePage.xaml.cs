using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class ClientCreatePage : ContentPage
{
    public ClientCreatePage(ClientCreateViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
