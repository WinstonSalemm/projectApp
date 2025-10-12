using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using ProjectApp.Client.Maui.Views;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = default!;
    private readonly IServiceProvider _services;
    private readonly AuthService _auth;

    public App(IServiceProvider services, AuthService auth)
    {
        InitializeComponent();
        _services = services;
        _auth = auth;
        Services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        if (_auth.IsAuthenticated)
        {
            var start = _services.GetRequiredService<SaleStartPage>();
            return new Window(new NavigationPage(start));
        }
        else
        {
            var select = _services.GetRequiredService<UserSelectPage>();
            return new Window(new NavigationPage(select));
        }
    }
}
