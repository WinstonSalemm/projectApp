using Microsoft.Maui.Controls;
using System.Windows.Input;

namespace ProjectApp.Client.Maui.Controls;

public partial class EmptyStateView : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(EmptyStateView), string.Empty);

    public static readonly BindableProperty DescriptionProperty =
        BindableProperty.Create(nameof(Description), typeof(string), typeof(EmptyStateView), string.Empty);

    public static readonly BindableProperty IconProperty =
        BindableProperty.Create(nameof(Icon), typeof(ImageSource), typeof(EmptyStateView), null);

    public static readonly BindableProperty ActionTextProperty =
        BindableProperty.Create(nameof(ActionText), typeof(string), typeof(EmptyStateView), string.Empty);

    public static readonly BindableProperty ActionCommandProperty =
        BindableProperty.Create(nameof(ActionCommand), typeof(ICommand), typeof(EmptyStateView), null);

    public static readonly BindableProperty ActionCommandParameterProperty =
        BindableProperty.Create(nameof(ActionCommandParameter), typeof(object), typeof(EmptyStateView), null);

    public EmptyStateView()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public ImageSource? Icon
    {
        get => (ImageSource?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public object? ActionCommandParameter
    {
        get => GetValue(ActionCommandParameterProperty);
        set => SetValue(ActionCommandParameterProperty, value);
    }

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public bool HasIcon => Icon is not null;
    public bool HasAction => !string.IsNullOrWhiteSpace(ActionText) && ActionCommand is not null;

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == DescriptionProperty.PropertyName)
            OnPropertyChanged(nameof(HasDescription));
        else if (propertyName == IconProperty.PropertyName)
            OnPropertyChanged(nameof(HasIcon));
        else if (propertyName == ActionTextProperty.PropertyName || propertyName == ActionCommandProperty.PropertyName)
            OnPropertyChanged(nameof(HasAction));
    }
}

