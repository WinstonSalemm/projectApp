using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.Models;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class SaleStartPage : ContentPage
{
    private readonly IServiceProvider _services;

    public SaleStartPage(SaleStartViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = vm;
        _services = services;
        SizeChanged += OnSizeChanged;
    }

    private async void OnCategoryClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not SaleStartViewModel vm)
        {
            return;
        }

        if (!vm.CanSelectSaleMethods)
        {
            await DisplayAlert("Сначала выберите менеджера", "Шаг 1: назначьте менеджера и точку продаж, затем выберите сценарий.", "OK");
            return;
        }

        if (vm.SelectedSaleMethod?.PaymentType is not PaymentType)
        {
            await DisplayAlert("Категории недоступны", "Категории используются только для быстрых продаж.", "OK");
            return;
        }

        string? category = null;
        if (sender is Button button && button.BindingContext is CategoryDto dto)
        {
            category = dto.Name;
        }

        await NavigateToQuickSaleAsync(vm, category);
    }

    private async void OnSaleMethodSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (BindingContext is not SaleStartViewModel vm)
        {
            return;
        }

        if (!vm.CanSelectSaleMethods)
        {
            if (sender is CollectionView collectionView)
            {
                collectionView.SelectedItem = null;
            }

            await DisplayAlert("Завершите шаг 1", "Выберите менеджера и точку продаж, чтобы продолжить.", "OK");
            return;
        }

        if (e.CurrentSelection?.FirstOrDefault() is not SaleMethodOption option)
        {
            return;
        }

        vm.ApplySaleMethod(option);
        await HandleSaleMethodAsync(vm, option);
    }

    private async Task HandleSaleMethodAsync(SaleStartViewModel vm, SaleMethodOption option)
    {
        if (option.Id == SaleMethodKind.CommissionClients)
        {
            await NavigateToClientsAsync();
            return;
        }

        switch (option.PaymentType)
        {
            case PaymentType.Return:
            {
                var history = _services.GetRequiredService<SalesHistoryPage>();
                if (history.BindingContext is SalesHistoryViewModel historyVm)
                {
                    historyVm.ShowAll = true;
                    if (historyVm.LoadCommand is IAsyncRelayCommand loadCommand)
                    {
                        await loadCommand.ExecuteAsync(null);
                    }
                }

                await Navigation.PushAsync(history);
                break;
            }
            case PaymentType.Contract:
            {
                var contracts = _services.GetRequiredService<ContractsListPage>();
                await Navigation.PushAsync(contracts);
                break;
            }
            default:
                await NavigateToQuickSaleAsync(vm);
                break;
        }
    }

    private async Task NavigateToQuickSaleAsync(SaleStartViewModel vm, string? categoryOverride = null)
    {
        var page = _services.GetRequiredService<QuickSalePage>();
        if (page.BindingContext is QuickSaleViewModel quickSaleVm)
        {
            if (vm.SelectedSaleMethod?.PaymentType is PaymentType paymentType)
            {
                quickSaleVm.SelectedPaymentType = paymentType;
            }

            quickSaleVm.SetPresetCategory(categoryOverride ?? vm.SelectedCategory?.Name);
        }

        await Navigation.PushAsync(page);
    }

    private async Task NavigateToClientsAsync()
    {
        var page = _services.GetRequiredService<ClientsListPage>();
        if (page.BindingContext is ClientsListViewModel clientsVm)
        {
            clientsVm.ShowOnlyMine = true;
            if (clientsVm.LoadCommand is IAsyncRelayCommand loadCommand)
            {
                await loadCommand.ExecuteAsync(null);
            }
        }

        await Navigation.PushAsync(page);
    }

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        var width = Width;
        if (width <= 0)
        {
            return;
        }

        var compact = GetBreakpoint("Breakpoint.Compact", 800);
        var medium = GetBreakpoint("Breakpoint.Medium", 1200);
        var state = width < compact ? "Compact" : width < medium ? "Medium" : "Expanded";
        VisualStateManager.GoToState(this, state);
    }

    private static double GetBreakpoint(string key, double fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is double d)
        {
            return d;
        }

        return fallback;
    }
}