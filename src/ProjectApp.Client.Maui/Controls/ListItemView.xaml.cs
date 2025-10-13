using Microsoft.Maui.Controls;
using System.Windows.Input;

namespace ProjectApp.Client.Maui.Controls;

public partial class ListItemView : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(ListItemView), string.Empty);

    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(ListItemView), string.Empty);

    public static readonly BindableProperty MetaProperty =
        BindableProperty.Create(nameof(Meta), typeof(string), typeof(ListItemView), string.Empty);

    public static readonly BindableProperty LeadingContentProperty =
        BindableProperty.Create(nameof(LeadingContent), typeof(View), typeof(ListItemView), null);

    public static readonly BindableProperty TrailingContentProperty =
        BindableProperty.Create(nameof(TrailingContent), typeof(View), typeof(ListItemView), null);

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(ListItemView), null);

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(ListItemView), null);

    public ListItemView()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string Meta
    {
        get => (string)GetValue(MetaProperty);
        set => SetValue(MetaProperty, value);
    }

    public View? LeadingContent
    {
        get => (View?)GetValue(LeadingContentProperty);
        set => SetValue(LeadingContentProperty, value);
    }

    public View? TrailingContent
    {
        get => (View?)GetValue(TrailingContentProperty);
        set => SetValue(TrailingContentProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);
    public bool HasMeta => !string.IsNullOrWhiteSpace(Meta);
    public bool HasLeading => LeadingContent is not null;
    public bool HasTrailing => TrailingContent is not null;

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == SubtitleProperty.PropertyName)
            OnPropertyChanged(nameof(HasSubtitle));
        else if (propertyName == MetaProperty.PropertyName)
            OnPropertyChanged(nameof(HasMeta));
        else if (propertyName == LeadingContentProperty.PropertyName)
            OnPropertyChanged(nameof(HasLeading));
        else if (propertyName == TrailingContentProperty.PropertyName)
            OnPropertyChanged(nameof(HasTrailing));
    }
}

