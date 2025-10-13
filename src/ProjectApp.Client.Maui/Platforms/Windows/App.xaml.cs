using Microsoft.Maui;

namespace ProjectApp.Client.Maui.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => ProjectApp.Client.Maui.MauiProgram.CreateMauiApp();
}

