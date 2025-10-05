using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.DependencyInjection;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.Views;

public partial class QuickSalePage : ContentPage
{
    private readonly IServiceProvider _services;

    public QuickSalePage(QuickSaleViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = vm;
        _services = services;

        // Add extra toolbar items based on role
        var auth = _services.GetRequiredService<AuthService>();
        // Returns available for Manager/Admin
        var returnsItem = new ToolbarItem { Text = "Возврат" };
        returnsItem.Clicked += OnReturnsClicked;
        ToolbarItems.Add(returnsItem);
        // Supplies only for Admin
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
        var page = _services.GetService<ReturnsPage>();
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
        var page = _services.GetService<ClientPickerPage>();
        if (page != null)
        {
            await Navigation.PushAsync(page);
        }
    }
}
