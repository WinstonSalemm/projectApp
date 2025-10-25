using System.Globalization;

namespace ProjectApp.Client.Maui.Converters;

public class RowColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int rowNumber)
        {
            return rowNumber % 2 == 0 ? Colors.White : Color.FromArgb("#f8f9fa");
        }
        return Colors.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
