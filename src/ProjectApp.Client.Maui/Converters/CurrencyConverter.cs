using System.Globalization;
using Microsoft.Maui.Controls;

namespace ProjectApp.Client.Maui.Converters;

// Formats money as: 12 345 UZS (always Uzbek sum text, no decimals)
public sealed class CurrencyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IFormattable formattable)
        {
            var formatted = formattable.ToString("N0", culture);
            return string.IsNullOrWhiteSpace(formatted) ? string.Empty : $"{formatted} UZS";
        }

        if (value == null)
        {
            return string.Empty;
        }

        if (decimal.TryParse(value.ToString(), NumberStyles.Any, culture, out var parsed))
        {
            return $"{parsed.ToString("N0", culture)} UZS";
        }

        return value.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

