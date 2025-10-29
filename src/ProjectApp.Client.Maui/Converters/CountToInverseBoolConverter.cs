using System.Globalization;

namespace ProjectApp.Client.Maui.Converters;

/// <summary>
/// Конвертер: Count == 0 → true (показать пустое состояние), иначе false
/// </summary>
public class CountToInverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count == 0;
        }
        return true; // Показываем empty state если что-то не так
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
