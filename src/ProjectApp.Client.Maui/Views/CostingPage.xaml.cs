using ProjectApp.Client.Maui.ViewModels;
#if WINDOWS
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
#endif

namespace ProjectApp.Client.Maui.Views;

public partial class CostingPage : ContentPage
{
    public CostingPage(CostingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
#if WINDOWS
        TryEnableHorizontalWheel();
#endif
    }

#if WINDOWS
    private bool _wheelHooked;
    private void TryEnableHorizontalWheel()
    {
        if (_wheelHooked) return;
        var platformView = DetailScroll?.Handler?.PlatformView as ScrollViewer;
        if (platformView is null) return;
        _wheelHooked = true;
        platformView.PointerWheelChanged += OnPointerWheelChanged;
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        var point = e.GetCurrentPoint(sv);
        var delta = point.Properties.MouseWheelDelta;
        const double step = 80;
        var newOffset = sv.HorizontalOffset - Math.Sign(delta) * step;
        if (newOffset < 0) newOffset = 0;
        sv.ChangeView(newOffset, null, null);
        e.Handled = true;
    }
#endif
}
