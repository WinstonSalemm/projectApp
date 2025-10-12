using System.Globalization;
using Microsoft.Maui.Controls;

namespace ProjectApp.Client.Maui.Converters;

public class DateTimeToRuConverter : IValueConverter
{
    // parameter: optional .NET format string, e.g. "g", "G", "d", or a custom pattern
    // default: "dd.MM.yyyy HH:mm"
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return string.Empty;
        try
        {
            var ru = CultureInfo.GetCultureInfo("ru-RU");
            var dt = System.Convert.ToDateTime(value, CultureInfo.InvariantCulture);
            var fmt = parameter as string;
            if (string.IsNullOrWhiteSpace(fmt)) fmt = "dd.MM.yyyy HH:mm";
            return dt.ToString(fmt, ru);
        }
        catch { return value?.ToString() ?? string.Empty; }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
