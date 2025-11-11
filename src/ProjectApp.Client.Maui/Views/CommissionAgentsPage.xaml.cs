using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class CommissionAgentsPage : ContentPage
{
    private readonly CommissionAgentsViewModel _viewModel;
    public event EventHandler<(int Id, string Name)>? CommissionAgentSelected;

    public CommissionAgentsPage(CommissionAgentsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAgentsAsync();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is CommissionAgentItemViewModel item)
        {
            (sender as CollectionView)!.SelectedItem = null;
            CommissionAgentSelected?.Invoke(this, (item.ClientId, item.ClientName));
        }
    }
}
