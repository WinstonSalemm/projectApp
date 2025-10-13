using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class ClientsListPage : ContentPage
{
    public ClientsListPage(ClientsListViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        var width = Width;
        if (width <= 0)
            return;

        var compact = GetBreakpoint("Breakpoint.Compact", 800);
        var medium = GetBreakpoint("Breakpoint.Medium", 1200);
        string state = width < compact ? "Compact" : width < medium ? "Medium" : "Expanded";
        VisualStateManager.GoToState(this, state);
    }

    private static double GetBreakpoint(string key, double fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true &&
            value is double d)
        {
            return d;
        }
        return fallback;
    }
}

