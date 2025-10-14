using System.Linq;
using Microsoft.Maui.Controls;

namespace ProjectApp.Client.Maui.Services;

public static class NavigationHelper
{
    private static Shell? CurrentShell => Shell.Current;

    public static INavigation? Navigation => CurrentShell?.Navigation ?? GetWindowNavigation();

    public static Page? GetCurrentPage()
    {
        if (CurrentShell?.CurrentPage is Page shellPage)
            return shellPage;

        return Application.Current?.Windows.FirstOrDefault()?.Page;
    }

    public static Task PushAsync(Page page)
    {
        var navigation = Navigation;
        return navigation != null ? navigation.PushAsync(page) : Task.CompletedTask;
    }

    public static Task PopAsync()
    {
        var navigation = Navigation;
        return navigation != null ? navigation.PopAsync() : Task.CompletedTask;
    }

    public static Task PushModalAsync(Page page, bool animated = true)
    {
        var navigation = Navigation;
        return navigation != null ? navigation.PushModalAsync(page, animated) : Task.CompletedTask;
    }

    public static Task PopModalAsync(bool animated = true)
    {
        var navigation = Navigation;
        return navigation != null ? navigation.PopModalAsync(animated) : Task.CompletedTask;
    }

    public static Task PopToRootAsync()
    {
        var navigation = Navigation;
        return navigation != null ? navigation.PopToRootAsync() : Task.CompletedTask;
    }

    public static Task DisplayAlert(string title, string message, string cancel)
    {
        if (CurrentShell != null)
        {
            return CurrentShell.DisplayAlert(title, message, cancel);
        }

        var window = Application.Current?.Windows.FirstOrDefault();
        if (window?.Page is Page page)
        {
            return page.DisplayAlert(title, message, cancel);
        }

        return Task.CompletedTask;
    }

    public static Task<bool> DisplayAlert(string title, string message, string accept, string cancel)
    {
        if (CurrentShell != null)
        {
            return CurrentShell.DisplayAlert(title, message, accept, cancel);
        }

        var window = Application.Current?.Windows.FirstOrDefault();
        if (window?.Page is Page page)
        {
            return page.DisplayAlert(title, message, accept, cancel);
        }

        return Task.FromResult(false);
    }

    public static async void SetRoot(Page page)
    {
        if (page is null)
            return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var app = Application.Current;
            if (app is null)
                return;

            // Close existing windows (single-window scenario)
            foreach (var win in app.Windows.ToList())
            {
                app.CloseWindow(win);
            }

            app.OpenWindow(new Window(page));
        });
    }

    private static INavigation? GetWindowNavigation()
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        return page?.Navigation;
    }
}

