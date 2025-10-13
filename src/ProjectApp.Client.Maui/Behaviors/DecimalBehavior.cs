using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Maui.Controls;

namespace ProjectApp.Client.Maui.Behaviors;

public class DecimalBehavior : Behavior<Entry>
{
    // allow digits and one decimal separator (comma or dot), up to 3 decimals
    private static readonly Regex Allowed = new("^[0-9]*([.,][0-9]{0,3})?$");
    private string _lastValid = "";

    protected override void OnAttachedTo(Entry bindable)
    {
        base.OnAttachedTo(bindable);
        bindable.TextChanged += OnTextChanged;
        _lastValid = bindable.Text ?? string.Empty;
    }

    protected override void OnDetachingFrom(Entry bindable)
    {
        base.OnDetachingFrom(bindable);
        bindable.TextChanged -= OnTextChanged;
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        var entry = (Entry)sender!;
        var text = e.NewTextValue ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            _lastValid = string.Empty;
            return;
        }
        if (Allowed.IsMatch(text))
        {
            _lastValid = text;
        }
        else
        {
            entry.Text = _lastValid;
        }
    }

    public static decimal ParseOrZero(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0m;
        var norm = text.Replace(',', '.');
        return decimal.TryParse(norm, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }
}

