using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class EditableSaleItem : ObservableObject
{
    [ObservableProperty] private int id;
    [ObservableProperty] private int productId;
    [ObservableProperty] private string sku = string.Empty;
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private decimal qty;
    [ObservableProperty] private decimal oldUnitPrice;
    [ObservableProperty] private decimal newUnitPrice;
    [ObservableProperty] private bool ndToImEligible;

    public decimal DeltaCashFlow => (OldUnitPrice - NewUnitPrice) * Qty;
    public bool HasDelta => NdToImEligible && NewUnitPrice != OldUnitPrice;

    partial void OnNewUnitPriceChanged(decimal value) { OnPropertyChanged(nameof(DeltaCashFlow)); OnPropertyChanged(nameof(HasDelta)); }
    partial void OnOldUnitPriceChanged(decimal value) { OnPropertyChanged(nameof(DeltaCashFlow)); OnPropertyChanged(nameof(HasDelta)); }
    partial void OnQtyChanged(decimal value) => OnPropertyChanged(nameof(DeltaCashFlow));
}

public partial class SaleEditViewModel : ObservableObject
{
    private readonly ApiSalesService _sales;

    [ObservableProperty] private bool isLoading;

    [ObservableProperty] private int id;
    [ObservableProperty] private int? clientId;
    [ObservableProperty] private string clientName = string.Empty;
    [ObservableProperty] private string paymentType = string.Empty;
    [ObservableProperty] private decimal total;
    [ObservableProperty] private DateTime createdAt;
    [ObservableProperty] private decimal totalCashFlow;
    [ObservableProperty] private bool hasChanges;

    public ObservableCollection<EditableSaleItem> Items { get; } = new();

    public SaleEditViewModel(ApiSalesService sales)
    {
        _sales = sales;
    }

    [RelayCommand]
    public async Task LoadAsync(int saleId)
    {
        if (IsLoading) return;
        try
        {
            IsLoading = true;
            var dto = await _sales.GetSaleByIdAsync(saleId);
            if (dto == null) return;
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                Id = dto.Id;
                ClientId = dto.ClientId;
                ClientName = dto.ClientName ?? string.Empty;
                PaymentType = dto.PaymentType;
                Total = dto.Total;
                CreatedAt = dto.CreatedAt;
                Items.Clear();
                foreach (var it in dto.Items)
                {
                    var e = new EditableSaleItem
                    {
                        Id = it.Id,
                        ProductId = it.ProductId,
                        Sku = it.Sku ?? string.Empty,
                        Name = it.Name ?? string.Empty,
                        Qty = it.Qty,
                        OldUnitPrice = it.UnitPrice,
                        NewUnitPrice = it.UnitPrice,
                        NdToImEligible = it.NdToImEligible
                    };
                    e.PropertyChanged += OnItemChanged;
                    Items.Add(e);
                }
                Recalc();
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditableSaleItem.NewUnitPrice) ||
            e.PropertyName == nameof(EditableSaleItem.OldUnitPrice) ||
            e.PropertyName == nameof(EditableSaleItem.Qty))
        {
            Recalc();
        }
    }

    private void Recalc()
    {
        TotalCashFlow = Items.Where(i => i.NdToImEligible)
            .Sum(i => (i.OldUnitPrice - i.NewUnitPrice) * i.Qty);
        HasChanges = Items.Any(i => i.NdToImEligible && i.NewUnitPrice != i.OldUnitPrice);
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (IsLoading) return;
        var changes = Items
            .Where(i => i.NdToImEligible && i.NewUnitPrice != i.OldUnitPrice)
            .Select(i => new ApiSalesService.RepriceItem { SaleItemId = i.Id, NewUnitPrice = i.NewUnitPrice })
            .ToList();
        if (changes.Count == 0) return;
        try
        {
            IsLoading = true;
            var resp = await _sales.RepriceNd2ImAsync(Id, changes, "both");
            if (resp != null)
            {
                await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    Total = resp.AdjustedTotal;
                    foreach (var upd in resp.UpdatedItems)
                    {
                        var item = Items.FirstOrDefault(x => x.Id == upd.Id);
                        if (item != null)
                        {
                            item.OldUnitPrice = item.NewUnitPrice;
                        }
                    }
                    Recalc();

                    try
                    {
                        var toast = Toast.Make("Сохранено", ToastDuration.Short);
                        await toast.Show();
                    }
                    catch { }

                    if (Microsoft.Maui.Controls.Shell.Current?.Navigation != null)
                    {
                        await Microsoft.Maui.Controls.Shell.Current.Navigation.PopAsync();
                    }
                });
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task CancelAsync()
    {
        if (IsLoading) return;
        try
        {
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (Microsoft.Maui.Controls.Shell.Current?.Navigation != null)
                {
                    await Microsoft.Maui.Controls.Shell.Current.Navigation.PopAsync();
                }
            });
        }
        catch { }
    }
}
