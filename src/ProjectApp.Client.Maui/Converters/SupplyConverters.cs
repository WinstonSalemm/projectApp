using System.Globalization;

namespace ProjectApp.Client.Maui.Converters;

// Конвертер для цвета таба
public class TabToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return Colors.Gray;

        var currentTab = value.ToString();
        var tabName = parameter.ToString();

        return currentTab == tabName ? Color.FromArgb("#3498db") : Color.FromArgb("#95a5a6");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Конвертер статуса поставки в текст
public class SupplyStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return "Неизвестно";

        var status = value.ToString();
        return status switch
        {
            "HasStock" => "✅ Есть товар",
            "Finished" => "🔴 Закончилась",
            _ => status
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Конвертер статуса в цвет
public class SupplyStatusColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return Colors.Gray;

        var status = value.ToString();
        return status switch
        {
            "HasStock" => Color.FromArgb("#27ae60"),
            "Finished" => Color.FromArgb("#e74c3c"),
            _ => Colors.Gray
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Проверка что поставка в ND-40
public class IsNd40Converter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string registerType)
            return registerType == "ND40";
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Конвертер для проверки IM-40
public class IsIm40Converter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string registerType)
            return registerType == "IM40";
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
