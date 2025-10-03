using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectApp.Client.Maui.Views;

public partial class ContractsListPage : ContentPage
{
    private readonly IServiceProvider _services;
    public ContractsListPage(ContractsListViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = vm;
        _services = services;
    }

    private async void OnCreateClicked(object? sender, EventArgs e)
    {
        var page = _services.GetService<ContractCreatePage>();
        if (page != null)
            await Navigation.PushAsync(page);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ContractsListViewModel vm)
        {
            try { await vm.RefreshAsync(); } catch { }
        }
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is int id)
        {
            var page = _services.GetService<ContractEditPage>();
            if (page != null)
            {
                if (page.BindingContext is ContractEditViewModel vm)
                {
                    await vm.LoadAsync(id);
                }
                await Navigation.PushAsync(page);
            }
        }
    }
}
