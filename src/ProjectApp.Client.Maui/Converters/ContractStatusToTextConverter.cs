using System.Globalization;
using Microsoft.Maui.Controls;

namespace ProjectApp.Client.Maui.Converters;

public class ContractStatusToTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = (value?.ToString() ?? string.Empty).Trim();
        return status switch
        {
            "Signed" => "Подписан",
            "Paid" => "Оплачен",
            "PartiallyClosed" => "Частично закрыт",
            "Cancelled" => "Отменён",
            "Closed" => "Закрыт",
            _ => status
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
