using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;
using System;
using ProjectApp.Client.Maui.Services;

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
            var debtor = e.CurrentSelection[0] as DebtorItemViewModel;
            if (debtor != null)
            {
                var page = App.Services.GetService<ProjectApp.Client.Maui.Views.ClientDetailPage>();
                if (page != null && page.BindingContext is ClientDetailViewModel vm)
                {
                    await vm.LoadAsync(debtor.ClientId);
                    await NavigationHelper.PushAsync(page);
                }
            }
            ((CollectionView)sender).SelectedItem = null;
        }
    }

    private async void OnCreateDebtClicked(object sender, EventArgs e)
    {
        // На первом шаге направим пользователя к выбору клиента
        var page = App.Services.GetService<ProjectApp.Client.Maui.Views.ClientsListPage>();
        if (page != null)
        {
            await NavigationHelper.PushAsync(page);
        }
    }
}
