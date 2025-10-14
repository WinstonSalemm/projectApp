using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ReturnForSaleViewModel : ObservableObject
{
    private readonly ApiSalesService _sales;
    private readonly ApiReturnsService _returnsApi;
    private readonly IReturnsService _returns;
    private readonly ApiCatalogService _catalog;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private int saleId;

    [ObservableProperty]
    private int? clientId;

    [ObservableProperty]
    private string clientName = string.Empty;

    [ObservableProperty]
    private string paymentType = string.Empty;

    [ObservableProperty]
    private DateTime createdAt;

    [ObservableProperty]
    private string? reason;

    public ObservableCollection<ReturnLine> Lines { get; } = new();

    // Если возврат уже существует, показываем кнопку "Отменить возврат"
    [ObservableProperty]
    private bool hasReturn;

    public string ActionButtonText => HasReturn ? "Отменить возврат" : "Оформить возврат";

    partial void OnHasReturnChanged(bool value) => OnPropertyChanged(nameof(ActionButtonText));

    public ReturnForSaleViewModel(ApiSalesService sales, ApiReturnsService returnsApi, IReturnsService returns, ApiCatalogService catalog)
    {
        _sales = sales;
        _returnsApi = returnsApi;
        _returns = returns;
        _catalog = catalog;
    }

    public partial class ReturnLine : ObservableObject
    {
        public int SaleItemId { get; set; }
        public int ProductId { get; set; }

        [ObservableProperty]
        private string sku = string.Empty;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private decimal soldQty;

        [ObservableProperty]
        private decimal alreadyReturnedQty;

        [ObservableProperty]
        private decimal availableQty;

        [ObservableProperty]
        private decimal returnQty;
    }

    public async Task LoadAsync(int saleId)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;

            var sale = await _sales.GetSaleByIdAsync(saleId);
            if (sale is null)
            {
                StatusMessage = $"Продажа #{saleId} не найдена.";
                return;
            }

            SaleId = sale.Id;
            ClientId = sale.ClientId;
            ClientName = sale.ClientName ?? string.Empty;
            PaymentType = sale.PaymentType;
            CreatedAt = sale.CreatedAt;

            var returns = await _returnsApi.GetBySaleAsync(saleId);
            HasReturn = returns.Any();

            var returnedMap = returns
                .SelectMany(r => r.Items)
                .GroupBy(i => i.SaleItemId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

            Lines.Clear();
            foreach (var item in sale.Items)
            {
                var sold = item.Qty;
                var alreadyReturned = returnedMap.TryGetValue(item.Id, out var done) ? done : 0m;
                var available = Math.Max(0m, sold - alreadyReturned);

                Lines.Add(new ReturnLine
                {
                    SaleItemId = item.Id,
                    ProductId = item.ProductId,
                    SoldQty = sold,
                    AlreadyReturnedQty = alreadyReturned,
                    AvailableQty = available,
                    ReturnQty = 0m
                });
            }

            var lookup = await _catalog.LookupAsync(Lines.Select(l => l.ProductId));
            foreach (var line in Lines)
            {
                if (lookup.TryGetValue(line.ProductId, out var info))
                {
                    line.Sku = info.Sku;
                    line.Name = info.Name;
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var line in Lines)
        {
            line.ReturnQty = line.AvailableQty;
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (SaleId <= 0)
        {
            StatusMessage = "Продажа не выбрана.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;

            if (HasReturn)
            {
                var confirm = await NavigationHelper.DisplayAlert(
                    "Отменить возврат",
                    "Отменить возврат по выбранной продаже?",
                    "Да",
                    "Нет");

                if (!confirm)
                {
                    return;
                }

                var cancelled = await _returns.CancelBySaleAsync(SaleId);
                StatusMessage = cancelled
                    ? "Возврат отменён."
                    : "Не удалось отменить возврат.";

                if (cancelled)
                {
                    HasReturn = false;
                    await LoadAsync(SaleId);
                }
            }
            else
            {
                var items = Lines
                    .Where(line => line.ReturnQty > 0)
                    .Select(line => new ReturnDraftItem
                    {
                        SaleItemId = line.SaleItemId,
                        Qty = line.ReturnQty
                    })
                    .ToList();

                var draft = new ReturnDraft
                {
                    RefSaleId = SaleId,
                    ClientId = ClientId,
                    Reason = Reason,
                    Items = items.Count == 0 ? null : items
                };

                var created = await _returns.CreateReturnAsync(draft);
                StatusMessage = created
                    ? "Возврат оформлен."
                    : "Не удалось оформить возврат.";

                if (created)
                {
                    HasReturn = true;
                    await NavigationHelper.DisplayAlert("Готово", "Возврат оформлен.", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
