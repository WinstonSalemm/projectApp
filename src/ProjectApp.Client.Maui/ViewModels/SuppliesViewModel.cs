using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;
using System.Collections.ObjectModel;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class SuppliesViewModel : ObservableObject
{
    private readonly ISuppliesService _supplies;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    // New supply item editor
    [ObservableProperty] private int newProductId;
    [ObservableProperty] private decimal newQty = 1m;
    [ObservableProperty] private decimal newUnitCost;
    [ObservableProperty] private string newCode = string.Empty;
    [ObservableProperty] private string? newNote;

    public ObservableCollection<SupplyDraftItem> Items { get; } = new();

    // Transfer section
    [ObservableProperty] private string transferCode = string.Empty;
    [ObservableProperty] private int transferProductId;
    [ObservableProperty] private decimal transferQty = 1m;
    public ObservableCollection<SupplyTransferItem> TransferItems { get; } = new();

    public SuppliesViewModel(ISuppliesService supplies)
    {
        _supplies = supplies;
    }

    [RelayCommand]
    private void AddItem()
    {
        if (NewProductId <= 0 || NewQty <= 0 || NewUnitCost < 0) return;
        Items.Add(new SupplyDraftItem
        {
            ProductId = NewProductId,
            Qty = NewQty,
            UnitCost = NewUnitCost,
            Code = NewCode ?? string.Empty,
            Note = NewNote
        });
        NewProductId = 0; NewQty = 1; NewUnitCost = 0; NewCode = string.Empty; NewNote = null;
    }

    [RelayCommand]
    private void RemoveItem(SupplyDraftItem? item)
    {
        if (item is null) return;
        Items.Remove(item);
    }

    [RelayCommand]
    private async Task CreateSupplyAsync()
    {
        if (Items.Count == 0) { StatusMessage = "Добавьте позиции"; return; }
        try
        {
            IsBusy = true; StatusMessage = string.Empty;
            var ok = await _supplies.CreateSupplyAsync(new SupplyDraft { Items = Items.ToList() });
            StatusMessage = ok ? "Поставка создана (ND-40)" : "Ошибка создания поставки";
            if (ok) Items.Clear();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void AddTransferItem()
    {
        if (string.IsNullOrWhiteSpace(TransferCode)) { StatusMessage = "Код обязателен"; return; }
        if (TransferProductId <= 0 || TransferQty <= 0) return;
        TransferItems.Add(new SupplyTransferItem { ProductId = TransferProductId, Qty = TransferQty });
        TransferProductId = 0; TransferQty = 1m;
    }

    [RelayCommand]
    private void RemoveTransferItem(SupplyTransferItem? item)
    {
        if (item is null) return;
        TransferItems.Remove(item);
    }

    [RelayCommand]
    private async Task TransferAsync()
    {
        if (string.IsNullOrWhiteSpace(TransferCode)) { StatusMessage = "Укажите код"; return; }
        if (TransferItems.Count == 0) { StatusMessage = "Добавьте позиции"; return; }
        try
        {
            IsBusy = true; StatusMessage = string.Empty;
            var ok = await _supplies.TransferToIm40Async(TransferCode.Trim(), TransferItems.ToList());
            StatusMessage = ok ? "Переведено в IM-40" : "Ошибка перевода";
            if (ok) TransferItems.Clear();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }
}
