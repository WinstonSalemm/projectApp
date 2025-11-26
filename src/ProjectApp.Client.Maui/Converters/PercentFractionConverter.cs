using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace ProjectApp.Client.Maui.Converters;

/// <summary>
/// Конвертер между долей (0.0025) во ViewModel и «человеческим» процентом (0.25) в UI.
/// VM хранит 0.0025, в Entry отображается 0.25.
/// </summary>
public class PercentFractionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal dec)
        {
            // VM: 0.0025 -> UI: 0.25
            var ui = dec * 100m;
            // Ограничиваем разумное число знаков, чтобы не плодить хвосты
            return ui.ToString("0.#######", culture);
        }

        if (value is double d)
        {
            var ui = (decimal)d * 100m;
            return ui.ToString("0.#######", culture);
        }

        if (value is float f)
        {
            var ui = (decimal)f * 100m;
            return ui.ToString("0.#######", culture);
        }

        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var s = value?.ToString();
        if (string.IsNullOrWhiteSpace(s))
            return 0m;

        var cleaned = s.Replace('%', ' ').Trim();

        if (!decimal.TryParse(cleaned, NumberStyles.Any, culture, out var ui) &&
            !decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out ui))
        {
            // Во время промежуточного ввода ("0.", "0,") не трогаем VM и не перерисовываем текст
            return Binding.DoNothing;
        }

        // UI: 0.25 -> VM: 0.0025
        var fraction = ui / 100m;

        if (targetType == typeof(double))
            return (double)fraction;
        if (targetType == typeof(float))
            return (float)fraction;

        return fraction;
    }
}
