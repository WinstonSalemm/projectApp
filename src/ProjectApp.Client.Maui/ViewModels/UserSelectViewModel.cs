using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class UserSelectViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly IServiceProvider _services;

    public UserSelectViewModel(AuthService auth, IServiceProvider services)
    {
        _auth = auth;
        _services = services;
    }

    [RelayCommand]
    private async Task LoginManagerAsync(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return;

        var ok = await _auth.LoginAsync(userName, null);
        if (!ok)
        {
            var detail = string.IsNullOrWhiteSpace(_auth.LastErrorMessage) ? string.Empty : $"\n{_auth.LastErrorMessage}";
            await NavigationHelper.DisplayAlert("Ошибка авторизации", $"Не удалось войти: {userName}.{detail}", "OK");
            return;
        }

        var targetRoute = string.Equals(_auth.Role, "Admin", StringComparison.OrdinalIgnoreCase)
            ? "dashboard"
            : "sales";

        var shell = _services.GetRequiredService<AppShell>();
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            NavigationHelper.SetRoot(shell);
            shell.RefreshRoleState();
            await shell.EnsureRouteAsync(targetRoute);
        });
    }

    [RelayCommand]
    private Task LoginShopAsync() => LoginManagerAsync("shop");

    [RelayCommand]
    private async Task LoginAdminAsync()
    {
        var login = _services.GetRequiredService<ProjectApp.Client.Maui.Views.LoginPage>();
        if (login.BindingContext is ProjectApp.Client.Maui.ViewModels.LoginViewModel lvm)
        {
            lvm.UserName = "admin";
            lvm.IsPasswordVisible = true;
        }

        await NavigationHelper.PushAsync(login);
    }
}

