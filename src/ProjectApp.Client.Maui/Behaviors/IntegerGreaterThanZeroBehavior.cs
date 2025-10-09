using System.Text.RegularExpressions;
using Microsoft.Maui.Controls;

namespace ProjectApp.Client.Maui.Behaviors;

public class IntegerGreaterThanZeroBehavior : Behavior<Entry>
{
    private static readonly Regex DigitsRegex = new("^[1-9][0-9]*$");
    private string _lastValid = "1";

    protected override void OnAttachedTo(Entry bindable)
    {
        base.OnAttachedTo(bindable);
        bindable.TextChanged += OnTextChanged;
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
        if (DigitsRegex.IsMatch(text))
        {
            _lastValid = text;
        }
        else
        {
            // revert to last valid text
            entry.Text = _lastValid;
        }
    }
}
