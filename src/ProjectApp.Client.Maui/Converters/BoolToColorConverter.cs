using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace ProjectApp.Client.Maui.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var isTrue = value is bool b && b;
            // True -> Error color (overdue), False -> Secondary text color
            var app = Application.Current;
            if (app?.Resources is not null)
            {
                var err = app.Resources.TryGetValue("Color.Error", out var errorObj) && errorObj is Color errorColor
                    ? errorColor
                    : Colors.Red;
                var sec = app.Resources.TryGetValue("Color.Text.Secondary", out var secObj) && secObj is Color secColor
                    ? secColor
                    : Colors.Gray;
                return isTrue ? err : sec;
            }
            return isTrue ? Colors.Red : Colors.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
