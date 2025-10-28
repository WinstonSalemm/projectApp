using System.Globalization;
using Microsoft.Maui.Controls;

namespace ProjectApp.Client.Maui.Converters;

public sealed class PercentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            return d.ToString("P1", culture);
        }

        if (value is decimal dec)
        {
            return dec.ToString("P1", culture);
        }

        if (value == null)
        {
            return string.Empty;
        }

        if (double.TryParse(value.ToString(), NumberStyles.Any, culture, out var parsed))
        {
            return parsed.ToString("P1", culture);
        }

        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
