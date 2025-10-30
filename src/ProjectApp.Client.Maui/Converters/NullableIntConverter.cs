using System;
using Microsoft.Maui.Controls;

namespace ProjectApp.Client.Maui.Converters;

public class NullableIntConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is int i) return i.ToString(culture);
        if (value is int?) return ((int?)value)?.ToString(culture);
        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        var s = value as string;
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (int.TryParse(s, System.Globalization.NumberStyles.Integer, culture, out var i)) return (int?)i;
        return null; // неверный ввод -> null, чтобы не падало
    }
}
