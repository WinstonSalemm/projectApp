using System.Globalization;
using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.Converters;

public class ClientTypeToRuConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClientType ct)
        {
            return ct switch
            {
                ClientType.Individual => "Физ. лицо",
                ClientType.Company => "Компания",
                _ => value.ToString() ?? string.Empty
            };
        }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
