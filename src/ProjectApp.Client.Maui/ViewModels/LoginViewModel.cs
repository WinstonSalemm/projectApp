using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private string userName = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private bool isPasswordVisible = true;

    public LoginViewModel(AuthService auth, IServiceProvider services)
    {
        _auth = auth;
        _services = services;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        var ok = await _auth.LoginAsync(UserName, string.IsNullOrWhiteSpace(Password) ? null : Password);
        if (!ok)
        {
            await NavigationHelper.DisplayAlert("Error", "Unable to sign in", "OK");
            return;
        }

        if (string.Equals(_auth.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            var shell = _services.GetRequiredService<AppShell>();
            shell.RefreshRoleState();
            NavigationHelper.SetRoot(shell);
        }
        else
        {
            var pay = _services.GetRequiredService<ProjectApp.Client.Maui.Views.PaymentSelectPage>();
            NavigationHelper.SetRoot(new NavigationPage(pay));
        }
    }
}
