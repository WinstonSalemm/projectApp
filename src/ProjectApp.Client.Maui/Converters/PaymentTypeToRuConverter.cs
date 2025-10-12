using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.Converters;

public class PaymentTypeToRuConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is PaymentType pt)
        {
            return pt switch
            {
                PaymentType.CashWithReceipt => "Наличными с чеком",
                PaymentType.CashNoReceipt => "Наличными без чека",
                PaymentType.CardWithReceipt => "Картой с чеком",
                PaymentType.ClickWithReceipt => "Click с чеком",
                PaymentType.ClickNoReceipt => "Click без чека",
                PaymentType.Click => "Click (старый)",
                PaymentType.Site => "Сайт",
                PaymentType.Return => "Возврат",
                PaymentType.Payme => "Payme",
                PaymentType.Contract => "Договор",
                _ => pt.ToString()
            };
        }
        return value?.ToString() ?? string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
