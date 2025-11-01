using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.Views;

public partial class ClientDetailPage : TabbedPage
{
    public ClientDetailPage(ClientDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private async void OnDebtSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count == 0) return;
        var item = e.CurrentSelection[0] as ApiClientsService.DebtListItem;
        ((CollectionView)sender).SelectedItem = null;
        if (item == null) return;

        var page = App.Services.GetService<ProjectApp.Client.Maui.Views.DebtDetailPage>();
        if (page != null && page.BindingContext is DebtDetailViewModel vm)
        {
            vm.DebtId = item.Id;
            await NavigationHelper.PushAsync(page);
        }
    }
}

