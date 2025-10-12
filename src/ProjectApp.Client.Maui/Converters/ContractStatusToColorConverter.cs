using System.Globalization;
using Microsoft.Maui.Controls;

namespace ProjectApp.Client.Maui.Converters;

public class ContractStatusToColorConverter : IValueConverter
{
    // ConverterParameter: "bg" | "border" | "text"
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = (value?.ToString() ?? string.Empty).Trim();
        var kind = (parameter?.ToString() ?? "bg").ToLowerInvariant();
        // Muted theme-aligned colors
        Color greenBg = Color.FromArgb("#E8F5EE");
        Color greenBorder = Color.FromArgb("#2F855A");
        Color greenText = Color.FromArgb("#1F5133");
        Color amberBg = Color.FromArgb("#FFF7E6");
        Color amberBorder = Color.FromArgb("#B7791F");
        Color amberText = Color.FromArgb("#7B4E0D");
        Color redBg = Color.FromArgb("#FBEAEA");
        Color redBorder = Color.FromArgb("#B91C1C");
        Color redText = Color.FromArgb("#7F1D1D");
        Color neutralBg = Color.FromArgb("#F5F7FA");
        Color neutralBorder = Color.FromArgb("#E2E8F0");
        Color neutralText = Color.FromArgb("#4B5563");

        (Color bg, Color border, Color text) colors = status switch
        {
            "Closed" => (greenBg, greenBorder, greenText),
            "Cancelled" => (redBg, redBorder, redText),
            "Paid" => (amberBg, amberBorder, amberText),
            "PartiallyClosed" => (amberBg, amberBorder, amberText),
            "Signed" => (amberBg, amberBorder, amberText),
            _ => (neutralBg, neutralBorder, neutralText)
        };

        return kind switch
        {
            "border" => colors.border,
            "text" => colors.text,
            _ => colors.bg
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
