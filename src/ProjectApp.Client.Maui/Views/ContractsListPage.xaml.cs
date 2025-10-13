using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using ProjectApp.Client.Maui.Services;

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

    private static ContractListItem? GetItemFromSender(object? sender)
    {
        if (sender is not BindableObject bo) return null;
        return bo.BindingContext as ContractListItem;
    }

    private async void OnPaidClicked(object? sender, EventArgs e)
    {
        var item = GetItemFromSender(sender);
        if (item == null) return;
        if (await DisplayAlert("Подтверждение", $"Отметить договор #{item.Id} как ОПЛАЧЕН?", "Да", "Отмена"))
        {
            if (BindingContext is ContractsListViewModel vm)
                await vm.MarkPaidAsync(item);
        }
    }

    private async void OnPartialClicked(object? sender, EventArgs e)
    {
        var item = GetItemFromSender(sender);
        if (item == null) return;
        if (await DisplayAlert("Подтверждение", $"Отметить договор #{item.Id} как ЧАСТИЧНО ЗАКРЫТ?", "Да", "Отмена"))
        {
            if (BindingContext is ContractsListViewModel vm)
                await vm.MarkPartiallyClosedAsync(item);
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        var item = GetItemFromSender(sender);
        if (item == null) return;
        if (await DisplayAlert("Подтверждение", $"ОТМЕНИТЬ договор #{item.Id}?", "Да", "Отмена"))
        {
            if (BindingContext is ContractsListViewModel vm)
                await vm.MarkCancelledAsync(item);
        }
    }

    private async void OnClosedClicked(object? sender, EventArgs e)
    {
        var item = GetItemFromSender(sender);
        if (item == null) return;
        if (await DisplayAlert("Подтверждение", $"Закрыть договор #{item.Id} ПОЛНОСТЬЮ?", "Да", "Отмена"))
        {
            if (BindingContext is ContractsListViewModel vm)
                await vm.MarkClosedAsync(item);
        }
    }
}

