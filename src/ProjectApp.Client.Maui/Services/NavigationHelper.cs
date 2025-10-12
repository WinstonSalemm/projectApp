using Microsoft.Maui.Controls;

namespace ProjectApp.Client.Maui.Services;

public static class NavigationHelper
{
    private static Window? CurrentWindow => Application.Current?.Windows?.FirstOrDefault();
    public static Page? CurrentPage => CurrentWindow?.Page;
    public static INavigation? CurrentNavigation => CurrentPage?.Navigation;

    public static void SetRoot(Page page)
    {
        var win = CurrentWindow;
        if (win is not null)
            win.Page = page;
        else if (Application.Current is Application app)
            app.OpenWindow(new Window(page));
    }

    public static Task PushAsync(Page page) => CurrentNavigation?.PushAsync(page) ?? Task.CompletedTask;
    public static Task<Page?> PopAsync() => CurrentNavigation != null ? CurrentNavigation.PopAsync() : Task.FromResult<Page?>(null);

    public static Task DisplayAlert(string title, string message, string cancel)
        => CurrentPage?.DisplayAlert(title, message, cancel) ?? Task.CompletedTask;

    public static Task<bool> DisplayAlert(string title, string message, string accept, string cancel)
        => CurrentPage?.DisplayAlert(title, message, accept, cancel) ?? Task.FromResult(false);
}
