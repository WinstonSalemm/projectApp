using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;
using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using ProjectApp.Client.Maui;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ReturnForSaleViewModel : ObservableObject
{
    private readonly ApiSalesService _sales;
    private readonly ApiReturnsService _returnsApi;
    private readonly IReturnsService _returns;
    private readonly ApiCatalogService _catalog;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage = string.Empty;

    [ObservableProperty] private int saleId;
    [ObservableProperty] private int? clientId;
    [ObservableProperty] private string clientName = string.Empty;
    [ObservableProperty] private string paymentType = string.Empty;
    [ObservableProperty] private DateTime createdAt;
    [ObservableProperty] private string? reason;

    public ObservableCollection<ReturnLine> Lines { get; } = new();

    // If a return already exists for the sale we show 'Отмена возврата'
    [ObservableProperty] private bool hasReturn;
    public string ActionButtonText => HasReturn ? "Отмена возврата" : "Оформить возврат";
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
        [ObservableProperty] private string sku = string.Empty;
        [ObservableProperty] private string name = string.Empty;
        [ObservableProperty] private decimal soldQty;
        [ObservableProperty] private decimal alreadyReturnedQty;
        [ObservableProperty] private decimal availableQty;
        [ObservableProperty] private decimal returnQty;
    }

    public async Task LoadAsync(int saleId)
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true; StatusMessage = string.Empty;
            var s = await _sales.GetSaleByIdAsync(saleId);
            if (s == null) { StatusMessage = $"Продажа #{saleId} не найдена"; return; }
            SaleId = s.Id; ClientId = s.ClientId; ClientName = s.ClientName; PaymentType = s.PaymentType; CreatedAt = s.CreatedAt;

            var returns = await _returnsApi.GetBySaleAsync(saleId);
            HasReturn = returns.Any();
            var returnedMap = returns
                .SelectMany(r => r.Items)
                .GroupBy(i => i.SaleItemId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

            Lines.Clear();
            foreach (var it in s.Items)
            {
                var sold = it.Qty;
                var done = returnedMap.TryGetValue(it.Id, out var d) ? d : 0m;
                var avail = Math.Max(0m, sold - done);
                Lines.Add(new ReturnLine
                {
                    SaleItemId = it.Id,
                    ProductId = it.ProductId,
                    SoldQty = sold,
                    AlreadyReturnedQty = done,
                    AvailableQty = avail,
                    ReturnQty = 0m
                });
            }

            // Load product names/SKUs for display
            var map = await _catalog.LookupAsync(Lines.Select(l => l.ProductId));
            foreach (var line in Lines)
            {
                if (map.TryGetValue(line.ProductId, out var info))
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
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var l in Lines)
            l.ReturnQty = l.AvailableQty;
    }
    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (SaleId <= 0) { StatusMessage = "Продажа не выбрана"; return; }
        try
        {
            IsBusy = true; StatusMessage = string.Empty;
            if (HasReturn)
            {
                var confirm = await NavigationHelper.DisplayAlert("Отмена возврата", "Отменить возврат по этой продаже?", "Да", "Нет");
                if (!confirm) return;
                var okCancel = await _returns.CancelBySaleAsync(SaleId);
                StatusMessage = okCancel ? "Возврат отменён" : "Не удалось отменить возврат";
                if (okCancel)
                {
                    HasReturn = false;
                    await LoadAsync(SaleId);
                    return;
                }
            }
            else
            {
                var items = Lines.Where(l => l.ReturnQty > 0).Select(l => new ReturnDraftItem
                {
                    SaleItemId = l.SaleItemId,
                    Qty = l.ReturnQty
                }).ToList();
                var draft = new ReturnDraft
                {
                    RefSaleId = SaleId,
                    ClientId = ClientId,
                    Reason = Reason,
                    Items = items.Count == 0 ? null : items
                };
                var ok = await _returns.CreateReturnAsync(draft);
                StatusMessage = ok ? "Возврат создан" : "Ошибка возврата";
                if (ok)
                {
                    HasReturn = true;
                    await NavigationHelper.DisplayAlert("OK", "Возврат создан", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }
}


