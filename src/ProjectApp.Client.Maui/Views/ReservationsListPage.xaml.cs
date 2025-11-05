using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectApp.Client.Maui.Views;

public partial class ReservationsListPage : ContentPage
{
    private readonly IServiceProvider _services;

    public ReservationsListPage(ReservationsListViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = vm;
        _services = services;
        Appearing += async (s, e) =>
        {
            if (BindingContext is ReservationsListViewModel lvm)
            {
                if (lvm.LoadCommand is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand cmd)
                {
                    await cmd.ExecuteAsync(null);
                }
                else
                {
                    await lvm.LoadAsync();
                }
            }
        };
    }

    private async void OnAddNewClicked(object sender, EventArgs e)
    {
        var page = _services.GetRequiredService<ReservationCreatePage>();
        await Navigation.PushAsync(page);
    }

    private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is not Services.ReservationListItem item)
        {
            return;
        }
        (sender as CollectionView)!.SelectedItem = null;
        var page = _services.GetRequiredService<ReservationDetailsPage>();
        if (page.BindingContext is ReservationDetailsViewModel vm)
        {
            await vm.LoadAsync(item.Id);
        }
        await Navigation.PushAsync(page);
    }
}
