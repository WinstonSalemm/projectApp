using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class ClientsListPage : ContentPage
{
    public ClientsListPage(ClientsListViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
