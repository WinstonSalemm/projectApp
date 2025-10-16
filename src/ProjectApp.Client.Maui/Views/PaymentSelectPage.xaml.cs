using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.Views;

public partial class PaymentSelectPage : ContentPage
{
    private readonly AuthService _auth;
    
    public PaymentSelectPage(ProjectApp.Client.Maui.ViewModels.PaymentSelectViewModel vm, AuthService auth)
    {
        InitializeComponent();
        BindingContext = vm;
        _auth = auth;
    }

    private void OnLogoutClicked(object? sender, EventArgs e)
    {
        _auth.Logout();
        var userSelectPage = App.Services.GetRequiredService<UserSelectPage>();
        NavigationHelper.SetRoot(new NavigationPage(userSelectPage));
    }
}

