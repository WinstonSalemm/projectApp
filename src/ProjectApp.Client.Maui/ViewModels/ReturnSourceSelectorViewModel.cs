using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;
using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ReturnSourceSelectorViewModel : ObservableObject
{
    private readonly ApiSalesService _sales;
    private readonly ApiContractsService _contracts; // Using concrete type to access GetContractsAsync
    private readonly AuthService _auth;
    private readonly IServiceProvider _services;

    public ObservableCollection<ReturnSourceItem> Items { get; } = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool hasNoItems = true;

    [ObservableProperty]
    private DateTime? dateFrom = DateTime.UtcNow.Date.AddDays(-31);

    [ObservableProperty]
    private DateTime? dateTo = DateTime.UtcNow.Date.AddDays(1);

    public ReturnSourceSelectorViewModel(
        ApiSalesService sales, 
        ApiContractsService contracts, // Using concrete type
        AuthService auth, 
        IServiceProvider services)
    {
        _sales = sales;
        _contracts = contracts;
        _auth = auth;
        _services = services;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        System.Diagnostics.Debug.WriteLine("[ReturnSourceSelectorViewModel] LoadAsync START");
        if (IsLoading) return;
        try
        {
            IsLoading = true;
            Items.Clear();

            // Загружаем продажи
            System.Diagnostics.Debug.WriteLine("[ReturnSourceSelectorViewModel] Loading sales...");
            var salesList = await _sales.GetSalesAsync(DateFrom, DateTo, createdBy: null, all: true);
            
            // Фильтруем только нужные типы продаж
            var validPaymentTypes = new[]
            {
                PaymentType.CashWithReceipt,
                PaymentType.CashNoReceipt,
                PaymentType.CardWithReceipt,
                PaymentType.ClickWithReceipt,
                PaymentType.ClickNoReceipt,
                PaymentType.Contract
            };

            foreach (var sale in salesList ?? Enumerable.Empty<ApiSalesService.SaleDto>())
            {
                Enum.TryParse<PaymentType>(sale.PaymentType, true, out var pt);
                
                if (!validPaymentTypes.Contains(pt))
                    continue;

                Items.Add(new ReturnSourceItem
                {
                    Id = sale.Id,
                    SourceType = ReturnSourceType.Sale,
                    ClientName = sale.ClientName,
                    Total = sale.Total,
                    CreatedAt = sale.CreatedAt,
                    CreatedBy = sale.CreatedBy,
                    PaymentType = pt
                });
            }

            // Загружаем договора
            System.Diagnostics.Debug.WriteLine("[ReturnSourceSelectorViewModel] Loading contracts...");
            var contractsList = await _contracts.GetContractsAsync(DateFrom, DateTo);
            
            foreach (var contract in contractsList ?? Enumerable.Empty<ApiContractsService.ContractDto>())
            {
                Items.Add(new ReturnSourceItem
                {
                    Id = contract.Id,
                    SourceType = ReturnSourceType.Contract,
                    ClientName = contract.OrgName, // Changed from ClientName to OrgName
                    Total = contract.TotalAmount, // Changed from Amount to TotalAmount
                    CreatedAt = contract.CreatedAt,
                    CreatedBy = contract.CreatedBy,
                    ContractNumber = contract.ContractNumber
                });
            }

            // Сортируем по дате (новые сверху)
            var sorted = Items.OrderByDescending(x => x.CreatedAt).ToList();
            Items.Clear();
            foreach (var item in sorted)
            {
                Items.Add(item);
            }

            HasNoItems = Items.Count == 0;
            System.Diagnostics.Debug.WriteLine($"[ReturnSourceSelectorViewModel] Loaded {Items.Count} items");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ReturnSourceSelectorViewModel] LoadAsync ERROR: {ex}");
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await App.Current!.MainPage!.DisplayAlert("Ошибка", $"Не удалось загрузить данные: {ex.Message}", "OK");
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task OpenReturnAsync(ReturnSourceItem? item)
    {
        if (item is null) return;

        try
        {
            if (item.SourceType == ReturnSourceType.Sale)
            {
                // Открываем страницу возврата по продаже
                var page = _services.GetService<ProjectApp.Client.Maui.Views.ReturnForSalePage>();
                if (page is null)
                {
                    await App.Current!.MainPage!.DisplayAlert("Ошибка", "Не удалось открыть страницу возврата", "OK");
                    return;
                }
                
                if (page.BindingContext is ProjectApp.Client.Maui.ViewModels.ReturnForSaleViewModel vm)
                {
                    await vm.LoadAsync(item.Id);
                }
                
                await NavigationHelper.PushAsync(page);
            }
            else if (item.SourceType == ReturnSourceType.Contract)
            {
                // TODO: Реализовать возврат по договору
                await App.Current!.MainPage!.DisplayAlert(
                    "В разработке", 
                    "Возврат по договорам будет реализован в следующей версии", 
                    "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ReturnSourceSelectorViewModel] OpenReturnAsync ERROR: {ex}");
            await App.Current!.MainPage!.DisplayAlert("Ошибка", $"Не удалось открыть возврат: {ex.Message}", "OK");
        }
    }
}
