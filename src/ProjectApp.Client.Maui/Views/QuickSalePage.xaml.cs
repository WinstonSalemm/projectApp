using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectApp.Client.Maui.Views;

public partial class QuickSalePage : ContentPage
{
    private readonly IServiceProvider _services;

    public QuickSalePage(QuickSaleViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = vm;
        _services = services;
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        var settingsPage = _services.GetService<SettingsPage>();
        if (settingsPage != null)
        {
            await Navigation.PushAsync(settingsPage);
        }
    }
}
