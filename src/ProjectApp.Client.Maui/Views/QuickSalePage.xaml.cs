using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.Services;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class QuickSalePage : ContentPage
{
    private readonly IServiceProvider _services;

    public QuickSalePage(QuickSaleViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = vm;
        _services = services;

        var auth = _services.GetRequiredService<AuthService>();

        var returnsItem = new ToolbarItem { Text = "Возвраты" };
        returnsItem.Clicked += OnReturnsClicked;
        ToolbarItems.Add(returnsItem);

        if (string.Equals(auth.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            var suppliesItem = new ToolbarItem { Text = "Поставки" };
            suppliesItem.Clicked += OnSuppliesClicked;
            ToolbarItems.Add(suppliesItem);

            var stocksItem = new ToolbarItem { Text = "Склад" };
            stocksItem.Clicked += OnStocksClicked;
            ToolbarItems.Add(stocksItem);

            var contractsItem = new ToolbarItem { Text = "Договоры" };
            contractsItem.Clicked += OnContractsClicked;
            ToolbarItems.Add(contractsItem);
        }
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        var settingsPage = _services.GetService<SettingsPage>();
        if (settingsPage != null)
        {
            await Navigation.PushAsync(settingsPage);
        }
    }

    private async void OnSuppliesClicked(object? sender, EventArgs e)
    {
        var page = _services.GetService<SuppliesPage>();
        if (page != null)
        {
            await Navigation.PushAsync(page);
        }
    }

    private async void OnReturnsClicked(object? sender, EventArgs e)
    {
        var page = _services.GetService<SalesHistoryPage>();
        if (page != null)
        {
            await Navigation.PushAsync(page);
        }
    }

    private async void OnStocksClicked(object? sender, EventArgs e)
    {
        var page = _services.GetService<StocksPage>();
        if (page != null)
        {
            await Navigation.PushAsync(page);
        }
    }

    private async void OnContractsClicked(object? sender, EventArgs e)
    {
        var page = _services.GetService<ContractsListPage>();
        if (page != null)
        {
            await Navigation.PushAsync(page);
        }
    }

    private async void OnPickClientClicked(object? sender, EventArgs e)
    {
        var page = _services.GetService<ClientsListPage>();
        if (page is null)
        {
            return;
        }

        if (page.BindingContext is ClientsListViewModel vm)
        {
            vm.ShowOnlyMine = true;
            if (vm.LoadCommand is IAsyncRelayCommand loadCommand)
            {
                await loadCommand.ExecuteAsync(null);
            }
        }

        await Navigation.PushAsync(page);
    }
}
