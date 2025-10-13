using System.Globalization;
using Microsoft.Maui.Controls;

namespace ProjectApp.Client.Maui.Converters;

public class QtyToAvailabilityColorConverter : IValueConverter
{
    // parameter: bg | border | text
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var qty = 0m;
        try { if (value is IConvertible c) qty = System.Convert.ToDecimal(c, CultureInfo.InvariantCulture); } catch { }
        var kind = (parameter?.ToString() ?? "bg").ToLowerInvariant();
        // Muted palette (aligned with App.xaml)
        var greenBg = Color.FromArgb("#E8F5EE");
        var greenBorder = Color.FromArgb("#2F855A");
        var greenText = Color.FromArgb("#1F5133");
        var grayBg = Color.FromArgb("#F5F7FA");
        var grayBorder = Color.FromArgb("#E2E8F0");
        var grayText = Color.FromArgb("#4B5563");

        var bg = qty > 0 ? greenBg : grayBg;
        var border = qty > 0 ? greenBorder : grayBorder;
        var text = qty > 0 ? greenText : grayText;

        return kind switch
        {
            "border" => border,
            "text" => text,
            _ => bg
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

