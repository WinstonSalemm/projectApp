
using System;
using System.Collections.Generic;
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

    private static readonly IReadOnlyDictionary<string, (string DisplayName, string Role)> Directory =
        new Dictionary<string, (string DisplayName, string Role)>(StringComparer.OrdinalIgnoreCase)
        {
            ["liliya"] = ("Лилия", "Manager"),
            ["timur"] = ("Тимур", "Manager"),
            ["albert"] = ("Альберт", "Manager"),
            ["alisher"] = ("Алишер", "Manager"),
            ["rasim"] = ("Расим", "Manager"),
            ["valeriy"] = ("Валерий", "Manager"),
            ["shop"] = ("Магазин", "Manager")
        };

    public UserSelectViewModel(AuthService auth, IServiceProvider services)
    {
        _auth = auth;
        _services = services;
    }

    [RelayCommand]
    private Task LoginShopAsync() => LoginManagerAsync("shop");

    [RelayCommand]
    private async Task LoginManagerAsync(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName) || !Directory.TryGetValue(userName, out var info))
        {
            await NavigationHelper.DisplayAlert("Неизвестный пользователь", "Выберите одного из доступных менеджеров.", "OK");
            return;
        }

        _auth.LoginOffline(userName, info.DisplayName, info.Role);
        await NavigateToShellAsync();
    }

    [RelayCommand]
    private async Task LoginAdminAsync()
    {
        var loginPage = _services.GetRequiredService<Views.LoginPage>();
        if (loginPage.BindingContext is LoginViewModel vm)
        {
            vm.UserName = "admin";
            vm.IsPasswordVisible = true;
        }

        await NavigationHelper.PushAsync(loginPage);
    }

    private async Task NavigateToShellAsync()
    {
        var targetRoute = string.Equals(_auth.Role, "Admin", StringComparison.OrdinalIgnoreCase)
            ? "dashboard"
            : "sales";

        try
        {
            var shell = _services.GetRequiredService<AppShell>();
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                NavigationHelper.SetRoot(shell);
                shell.RefreshRoleState();
                await shell.EnsureRouteAsync(targetRoute);
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await NavigationHelper.DisplayAlert("Ошибка загрузки интерфейса", ex.Message, "OK");
            });
        }
    }
}
