using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;
using System;

namespace ProjectApp.Client.Maui.Views;

public partial class DebtorsListPage : ContentPage
{
    private readonly DebtorsListViewModel _viewModel;

    public DebtorsListPage(DebtorsListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadDebtorsAsync();
    }

    private async void OnDebtorSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count > 0)
        {
            var debtor = e.CurrentSelection[0];
            // Navigate to debtor detail
            // TODO: Implement navigation to ClientDetailPage with debtorId
            ((CollectionView)sender).SelectedItem = null;
        }
    }
}
