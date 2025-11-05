
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
        Console.WriteLine("[UserSelectVM] ctor");
        _auth = auth;
        _services = services;
    }

    [RelayCommand]
    private Task LoginShopAsync() => LoginManagerAsync("shop");

    [RelayCommand]
    private async Task LoginManagerAsync(string? userName)
    {
        Console.WriteLine($"[UserSelectVM] LoginManagerAsync invoked with userName={userName}");
        if (string.IsNullOrWhiteSpace(userName) || !Directory.TryGetValue(userName, out var info))
        {
            await NavigationHelper.DisplayAlert("Неизвестный пользователь", "Выберите одного из доступных менеджеров.", "OK");
            return;
        }

        // Login through API to get real token (password=null for managers)
        Console.WriteLine($"[UserSelectVM] Attempting API login for {userName}");
        var success = await _auth.LoginAsync(userName, null);
        if (!success)
        {
            Console.WriteLine($"[UserSelectVM] Login failed: {_auth.LastErrorMessage}");
            await NavigationHelper.DisplayAlert("Ошибка", $"Не удалось войти. {_auth.LastErrorMessage}", "OK");
            return;
        }
        Console.WriteLine($"[UserSelectVM] Login succeeded, role={_auth.Role}, displayName={_auth.DisplayName}");
        // Manager goes directly to payment selection, NOT to shell with tabs
        var paymentPage = _services.GetRequiredService<Views.PaymentSelectPage>();
        NavigationHelper.SetRoot(new NavigationPage(paymentPage));
    }

    [RelayCommand]
    private async Task LoginAdminAsync()
    {
        try
        {
            // Show password prompt dialog
            var mainPage = Application.Current?.MainPage;
            if (mainPage == null)
                return;

            var password = await mainPage.DisplayPromptAsync(
                "Вход администратора",
                "Введите пароль:",
                "Войти",
                "Отмена",
                placeholder: "Пароль",
                maxLength: 50,
                keyboard: Keyboard.Default,
                initialValue: "");

            if (string.IsNullOrWhiteSpace(password))
                return;

            // Try to login with admin credentials
            var success = await _auth.LoginAsync("admin", password);
            if (!success)
            {
                await NavigationHelper.DisplayAlert("Ошибка", $"Неверный пароль. {_auth.LastErrorMessage}", "OK");
                return;
            }

            // Navigate to simple admin page (temporarily instead of AppShell)
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var adminPage = _services.GetRequiredService<Views.SimpleAdminPage>();
                NavigationHelper.SetRoot(new NavigationPage(adminPage));
            });
        }
        catch (Exception ex)
        {
            await NavigationHelper.DisplayAlert("Ошибка входа", $"Не удалось войти: {ex.Message}", "OK");
        }
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
