using Microsoft.Maui.Controls;

namespace ProjectApp.Client.Maui.Controls;

public partial class TopAppBar : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(TopAppBar), string.Empty);

    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(TopAppBar), string.Empty);

    public static readonly BindableProperty ActionContentProperty =
        BindableProperty.Create(nameof(ActionContent), typeof(View), typeof(TopAppBar), propertyChanged: OnActionContentChanged);

    public TopAppBar()
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

    public View? ActionContent
    {
        get => (View?)GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }

    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    public bool HasActionContent => ActionContent is not null;

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == SubtitleProperty.PropertyName)
        {
            OnPropertyChanged(nameof(HasSubtitle));
        }
        else if (propertyName == ActionContentProperty.PropertyName)
        {
            OnPropertyChanged(nameof(HasActionContent));
        }
    }

    private static void OnActionContentChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is TopAppBar bar)
        {
            bar.OnPropertyChanged(nameof(HasActionContent));
        }
    }
}

