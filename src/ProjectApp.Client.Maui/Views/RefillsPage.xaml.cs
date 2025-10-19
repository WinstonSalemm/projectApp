using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class RefillsPage : ContentPage
{
    private readonly RefillsViewModel _viewModel;

    public RefillsPage(RefillsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Загрузить данные при открытии страницы
        await _viewModel.LoadRefillsCommand.ExecuteAsync(null);
    }
}
