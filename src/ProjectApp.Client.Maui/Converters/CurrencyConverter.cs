using System.Globalization;
using Microsoft.Maui.Controls;

namespace ProjectApp.Client.Maui.Converters;

// Formats money as: 12 345 сум (always Uzbek sum text, no decimals)
public class CurrencyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return string.Empty;
        try
        {
            var dec = System.Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            // Format with thousands separators and 0 decimals
            var formatted = dec.ToString("N0", culture);
            return formatted + " сум";
        }
        catch { return value?.ToString() + " сум" ?? string.Empty; }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
