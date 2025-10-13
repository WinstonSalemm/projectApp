using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectApp.Client.Maui.Views;

public partial class UserSelectPage : ContentPage
{
    private readonly IServiceProvider _services;
    public UserSelectPage(ProjectApp.Client.Maui.ViewModels.UserSelectViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = vm;
        _services = services;
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        var settings = _services.GetService<SettingsPage>();
        if (settings != null)
            await Navigation.PushAsync(settings);
    }
}

