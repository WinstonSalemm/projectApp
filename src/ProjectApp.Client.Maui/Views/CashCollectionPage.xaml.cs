using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class CashCollectionPage : ContentPage
{
    private readonly CashCollectionViewModel _viewModel;

    public CashCollectionPage(CashCollectionViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Загрузить данные при открытии страницы
        await _viewModel.LoadDataCommand.ExecuteAsync(null);
    }
}
