using System.Globalization;
using Microsoft.Maui.Controls;

namespace ProjectApp.Client.Maui.Converters;

public class CurrencyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return string.Empty;
        try
        {
            var dec = System.Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            return dec.ToString("C", CultureInfo.CurrentCulture);
        }
        catch { return value?.ToString() ?? string.Empty; }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
