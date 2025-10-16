namespace ProjectApp.Client.Maui.Views;

public partial class AnalyticsMenuPage : ContentPage
{
    public AnalyticsMenuPage()
    {
        InitializeComponent();
    }

    private async void OnFinancesTapped(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new FinanceAnalyticsPage());
    }

    private async void OnManagersTapped(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new ManagerAnalyticsPage());
    }

    private async void OnProductCostsTapped(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new ProductCostsPage());
    }

    private void OnCardPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Border border)
        {
            // Увеличиваем масштаб и тень при наведении
            border.ScaleTo(1.02, 150, Easing.CubicOut);
            border.Shadow = new Shadow
            {
                Brush = Colors.Black,
                Opacity = 0.2f,
                Radius = 12,
                Offset = new Point(0, 6)
            };
        }
    }

    private void OnCardPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Border border)
        {
            // Возвращаем в исходное состояние
            border.ScaleTo(1.0, 150, Easing.CubicOut);
            border.Shadow = new Shadow
            {
                Brush = Colors.Black,
                Opacity = 0.1f,
                Radius = 8,
                Offset = new Point(0, 4)
            };
        }
    }
}
