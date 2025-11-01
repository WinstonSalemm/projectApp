using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace ProjectApp.Client.Maui.Converters;

public class IsZeroConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            if (value == null) return false;
            if (value is int i) return i == 0;
            if (value is long l) return l == 0L;
            if (value is float f) return Math.Abs(f) < 1e-6;
            if (value is double d) return Math.Abs(d) < 1e-9;
            if (value is decimal m) return m == 0m;
            if (decimal.TryParse(value.ToString(), out var dm)) return dm == 0m;
        }
        catch { }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
