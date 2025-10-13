using System.Globalization;
using Microsoft.Maui.Controls;

namespace ProjectApp.Client.Maui.Converters;

public class PaymentTypeToColorConverter : IValueConverter
{
    // ConverterParameter: "bg" | "border" | "text"
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var pt = (value?.ToString() ?? string.Empty).Trim();
        var kind = (parameter?.ToString() ?? "bg").ToLowerInvariant();

        // Muted palette aligned with new theme
        var greenBg = Color.FromArgb("#E8F5EE");
        var greenBorder = Color.FromArgb("#2F855A");
        var greenText = Color.FromArgb("#1F5133");

        var amberBg = Color.FromArgb("#FFF7E6");
        var amberBorder = Color.FromArgb("#B7791F");
        var amberText = Color.FromArgb("#7B4E0D");

        var blueBg = Color.FromArgb("#EEF2FB");
        var blueBorder = Color.FromArgb("#365A8C");
        var blueText = Color.FromArgb("#2C3E64");

        var grayBg = Color.FromArgb("#F5F7FA");
        var grayBorder = Color.FromArgb("#E2E8F0");
        var grayText = Color.FromArgb("#4B5563");

        (Color bg, Color border, Color text) colors = pt switch
        {
            // With receipt => green
            "CashWithReceipt" or "CardWithReceipt" or "ClickWithReceipt" => (greenBg, greenBorder, greenText),
            // Without receipt => amber
            "CashNoReceipt" or "ClickNoReceipt" => (amberBg, amberBorder, amberText),
            // Reservation => blue
            "Reservation" => (blueBg, blueBorder, blueText),
            _ => (grayBg, grayBorder, grayText)
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

