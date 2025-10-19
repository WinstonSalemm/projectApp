using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class DefectivesPage : ContentPage
{
    private readonly DefectivesViewModel _viewModel;

    public DefectivesPage(DefectivesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Загрузить данные при открытии страницы
        await _viewModel.LoadDefectivesCommand.ExecuteAsync(null);
    }
}
