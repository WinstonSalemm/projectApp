using System;
using Microsoft.Maui.Controls;

namespace ProjectApp.Client.Maui.Behaviors;

public static class PointerCursor
{
    public static readonly BindableProperty IsEnabledProperty = BindableProperty.CreateAttached(
        "IsEnabled",
        typeof(bool),
        typeof(PointerCursor),
        false,
        propertyChanged: OnIsEnabledChanged);

    public static bool GetIsEnabled(BindableObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(BindableObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not VisualElement ve) return;
#if WINDOWS
        ve.HandlerChanged -= OnHandlerChanged;
        if (newValue is bool enabled && enabled)
        {
            ve.HandlerChanged += OnHandlerChanged;
            // If handler already present, wire events immediately
            if (ve.Handler is not null)
            {
                WireWindowsCursor(ve);
            }
        }
        else
        {
            UnwireWindowsCursor(ve);
        }
#endif
    }

#if WINDOWS
    private static void OnHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is VisualElement ve)
        {
            WireWindowsCursor(ve);
        }
    }

    private static void WireWindowsCursor(VisualElement ve)
    {
        try
        {
            if (ve.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement fe)
            {
                fe.PointerEntered -= OnPointerEntered;
                fe.PointerExited  -= OnPointerExited;
                fe.PointerMoved   -= OnPointerMoved;
                fe.PointerEntered += OnPointerEntered;
                fe.PointerExited  += OnPointerExited;
                fe.PointerMoved   += OnPointerMoved;
            }
        }
        catch { }
    }

    private static void UnwireWindowsCursor(VisualElement ve)
    {
        try
        {
            if (ve.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement fe)
            {
                fe.PointerEntered -= OnPointerEntered;
                fe.PointerExited  -= OnPointerExited;
                fe.PointerMoved   -= OnPointerMoved;
            }
        }
        catch { }
    }

    private static void SetProtectedCursor(Microsoft.UI.Xaml.FrameworkElement fe, Microsoft.UI.Input.InputSystemCursorShape? shape)
    {
        try
        {
            var pi = typeof(Microsoft.UI.Xaml.FrameworkElement).GetProperty("ProtectedCursor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (pi is null) return;
            object? cursor = null;
            if (shape.HasValue)
            {
                cursor = Microsoft.UI.Input.InputSystemCursor.Create(shape.Value);
            }
            pi.SetValue(fe, cursor);
        }
        catch { }
    }

    private static void OnPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.FrameworkElement fe)
            SetProtectedCursor(fe, Microsoft.UI.Input.InputSystemCursorShape.Hand);
    }

    private static void OnPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.FrameworkElement fe)
            SetProtectedCursor(fe, Microsoft.UI.Input.InputSystemCursorShape.Hand);
    }

    private static void OnPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.FrameworkElement fe)
            SetProtectedCursor(fe, null);
    }
#endif
}
