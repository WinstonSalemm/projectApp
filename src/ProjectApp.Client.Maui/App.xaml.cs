using Microsoft.Maui.Controls;
using Microsoft.Maui;
using Microsoft.Maui.Graphics;
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
        
        // Always logout on app start so user must login again
        _auth.Logout();
        
        ApplyAppThemePalette();

        // Global UI exception handlers to avoid hard crash in debug and show message
        try
        {
#if WINDOWS
            // WinUI UI-thread exceptions
            Microsoft.UI.Xaml.Application.Current.UnhandledException += async (sender, e) =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[App] WinUI.UnhandledException: {e.Message} : {e.Exception}");
                    await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await ProjectApp.Client.Maui.Services.NavigationHelper.DisplayAlert(
                            "Необработанное исключение",
                            e.Exception?.Message ?? e.Message ?? "Неизвестная ошибка",
                            "OK");
                    });
                }
                catch { }

                try { e.Handled = true; } catch { }
            };
#endif
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                try { System.Diagnostics.Debug.WriteLine($"[App] UnobservedTaskException: {e.Exception}"); e.SetObserved(); } catch { }
            };
            System.AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try { System.Diagnostics.Debug.WriteLine($"[App] AppDomain.UnhandledException: {e.ExceptionObject}"); } catch { }
            };
        }
        catch { }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        if (_auth.IsAuthenticated && string.Equals(_auth.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            // Admin goes to shell with tabs
            var shell = _services.GetRequiredService<AppShell>();
            return new Window(shell);
        }
        else if (_auth.IsAuthenticated)
        {
            // Manager goes to payment selection without tabs
            var paymentPage = _services.GetRequiredService<PaymentSelectPage>();
            return new Window(new NavigationPage(paymentPage));
        }
        else
        {
            // Not authenticated - show user select
            var select = _services.GetRequiredService<UserSelectPage>();
            return new Window(new NavigationPage(select));
        }
    }

    private void ApplyAppThemePalette()
    {
        ApplyPalette(RequestedTheme);
        RequestedThemeChanged += OnRequestedThemeChanged;
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs args)
    {
        ApplyPalette(args.RequestedTheme);
    }

    private void ApplyPalette(AppTheme theme)
    {
        bool isDark = theme == AppTheme.Dark;
        SetColor("Color.Primary", isDark ? "Palette.Primary.Dark" : "Palette.Primary.Light");
        SetColor("Color.OnPrimary", isDark ? "Palette.OnPrimary.Dark" : "Palette.OnPrimary.Light");
        SetColor("Color.Primary.Muted", isDark ? "Palette.PrimaryMuted.Dark" : "Palette.PrimaryMuted.Light");
        SetColor("Color.Secondary", isDark ? "Palette.Secondary.Dark" : "Palette.Secondary.Light");
        SetColor("Color.OnSecondary", isDark ? "Palette.OnSecondary.Dark" : "Palette.OnSecondary.Light");
        SetColor("Color.Accent", isDark ? "Palette.Accent.Dark" : "Palette.Accent.Light");
        SetColor("Color.OnAccent", isDark ? "Palette.OnAccent.Dark" : "Palette.OnAccent.Light");
        SetColor("Color.Background", isDark ? "Palette.Background.Dark" : "Palette.Background.Light");
        SetColor("Color.Surface", isDark ? "Palette.Surface.Dark" : "Palette.Surface.Light");
        SetColor("Color.Surface.Alt", isDark ? "Palette.SurfaceAlt.Dark" : "Palette.SurfaceAlt.Light");
        SetColor("Color.Surface.Muted", isDark ? "Palette.SurfaceMuted.Dark" : "Palette.SurfaceMuted.Light");
        SetColor("Color.Border", isDark ? "Palette.Border.Dark" : "Palette.Border.Light");
        SetColor("Color.BorderStrong", isDark ? "Palette.BorderStrong.Dark" : "Palette.BorderStrong.Light");
        SetColor("Color.Outline", isDark ? "Palette.Outline.Dark" : "Palette.Outline.Light");
        SetColor("Color.Text.Primary", isDark ? "Palette.Text.Primary.Dark" : "Palette.Text.Primary.Light");
        SetColor("Color.Text.Secondary", isDark ? "Palette.Text.Secondary.Dark" : "Palette.Text.Secondary.Light");
        SetColor("Color.Text.Tertiary", isDark ? "Palette.Text.Tertiary.Dark" : "Palette.Text.Tertiary.Light");
        SetColor("Color.Text.Inverse", isDark ? "Palette.Text.Inverse.Dark" : "Palette.Text.Inverse.Light");
        SetColor("Color.Success", isDark ? "Palette.Success.Dark" : "Palette.Success.Light");
        SetColor("Color.Warning", isDark ? "Palette.Warning.Dark" : "Palette.Warning.Light");
        SetColor("Color.Error", isDark ? "Palette.Error.Dark" : "Palette.Error.Light");
    }

    private void SetColor(string key, string sourceKey)
    {
        if (Resources.TryGetValue(sourceKey, out var value) && value is Color color)
        {
            Resources[key] = color;
        }
    }

}

