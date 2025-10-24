using System.Globalization;

namespace ProjectApp.Client.Maui.Converters;

public class BoolToExpandCollapseConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? "▲" : "▼";
        }
        return "▼";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
