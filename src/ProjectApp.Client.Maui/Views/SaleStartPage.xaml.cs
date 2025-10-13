using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class SaleStartPage : ContentPage
{
    private readonly IServiceProvider _services;

    public SaleStartPage(SaleStartViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = vm;
        _services = services;
        SizeChanged += OnSizeChanged;
    }

    private async void OnCategoryClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not SaleStartViewModel vm) return;
        string? category = null;
        if (sender is Button btn)
            category = btn.BindingContext as string;
        var qs = _services.GetRequiredService<QuickSalePage>();
        if (qs.BindingContext is QuickSaleViewModel qvm)
        {
            qvm.SelectedPaymentType = vm.SelectedPaymentType;
            qvm.SetPresetCategory(category);
        }
        await Navigation.PushAsync(qs);
    }

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        var width = Width;
        if (width <= 0)
            return;

        var compact = GetBreakpoint("Breakpoint.Compact", 800);
        var medium = GetBreakpoint("Breakpoint.Medium", 1200);
        var state = width < compact ? "Compact" : width < medium ? "Medium" : "Expanded";
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

