using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using ProjectApp.Client.Maui.Views;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui;

public partial class App : Application
{
    public App(IServiceProvider services, AuthService auth)
    {
        InitializeComponent();
        if (auth.IsAuthenticated)
        {
            var qs = services.GetRequiredService<QuickSalePage>();
            MainPage = new NavigationPage(qs);
        }
        else
        {
            var select = services.GetRequiredService<UserSelectPage>();
            MainPage = new NavigationPage(select);
        }
    }
}
