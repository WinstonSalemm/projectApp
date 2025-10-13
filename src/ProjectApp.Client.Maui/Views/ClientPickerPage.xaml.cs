using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class ClientPickerPage : ContentPage
{
    private readonly IServiceProvider _services;

    public ClientPickerPage(ClientPickerViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = vm;
        _services = services;
    }

    private async void OnCreateClicked(object? sender, EventArgs e)
    {
        var page = _services.GetService<ClientCreatePage>();
        if (page != null)
        {
            await Navigation.PushAsync(page);
        }
    }
}

