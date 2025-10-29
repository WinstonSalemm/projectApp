using System.Globalization;

namespace ProjectApp.Client.Maui.Converters;

public class StringEqualToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) 
            return Color.FromArgb("#94A3B8"); // Slate-400 - неактивная
        
        bool isEqual = value.ToString() == parameter.ToString();
        return isEqual 
            ? Color.FromArgb("#3B82F6") // Blue-500 - активная
            : Color.FromArgb("#94A3B8"); // Slate-400 - неактивная
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
