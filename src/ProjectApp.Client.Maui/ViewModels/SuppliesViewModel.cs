using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using ProjectApp.Client.Maui;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class SuppliesViewModel : ObservableObject
{
    private readonly ISuppliesService _supplies;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    // === ПАРАМЕТРЫ РАСЧЕТА НД-40 ===
    [ObservableProperty]
    private bool showCostParams = true;

    [ObservableProperty]
    private decimal exchangeRate = 158.08m;

    [ObservableProperty]
    private decimal customsFee = 105000m;

    [ObservableProperty]
    private decimal vatPercent = 22m;

    [ObservableProperty]
    private decimal correctionPercent = 0.50m;

    [ObservableProperty]
    private decimal securityPercent = 0.2m;

    [ObservableProperty]
    private decimal declarationPercent = 1m;

    [ObservableProperty]
    private decimal certificationPercent = 1m;

    [ObservableProperty]
    private decimal calculationBase = 10000000m;

    [ObservableProperty]
    private decimal loadingPercent = 1.6m;

    // === ПРЕДВАРИТЕЛЬНЫЙ РАСЧЕТ ===
    [ObservableProperty]
    private bool hasCostPreview;

    [ObservableProperty]
    private decimal grandTotalCost;

    public ObservableCollection<CostPreviewItemVm> CostPreviewItems { get; } = new();

    // New supply item editor
    [ObservableProperty] private int newProductId;
    [ObservableProperty] private string? newProductSku;
    [ObservableProperty] private string? newProductName;
    [ObservableProperty] private decimal newQty = 1m;
    [ObservableProperty] private decimal newUnitCost;
    [ObservableProperty] private string newCode = string.Empty;
    [ObservableProperty] private string? newNote;

    public decimal NewLineTotal => NewQty * NewUnitCost;
    partial void OnNewQtyChanged(decimal value) => OnPropertyChanged(nameof(NewLineTotal));
    partial void OnNewUnitCostChanged(decimal value) => OnPropertyChanged(nameof(NewLineTotal));

    // Stocks for selected product (supply editor)
    [ObservableProperty] private decimal selectedNd40Qty;
    [ObservableProperty] private decimal selectedIm40Qty;
    [ObservableProperty] private decimal selectedTotalQty;

    public ObservableCollection<SupplyDraftItem> Items { get; } = new();

    // Transfer section
    [ObservableProperty] private string transferCode = string.Empty;
    [ObservableProperty] private int transferProductId;
    [ObservableProperty] private string? transferProductSku;
    [ObservableProperty] private string? transferProductName;
    [ObservableProperty] private decimal transferQty = 1m;
    public ObservableCollection<SupplyTransferItem> TransferItems { get; } = new();

    // Stocks for selected product (transfer editor)
    [ObservableProperty] private decimal transferNd40Qty;
    [ObservableProperty] private decimal transferIm40Qty;
    [ObservableProperty] private decimal transferTotalQty;

    public SuppliesViewModel(ISuppliesService supplies)
    {
        _supplies = supplies;
        _ = LoadDefaultsAsync();
    }

    private async Task LoadDefaultsAsync()
    {
        try
        {
            var defaults = await _supplies.GetCostDefaultsAsync();
            if (defaults != null)
            {
                if (defaults.TryGetValue("ExchangeRate", out var rate)) ExchangeRate = rate;
                if (defaults.TryGetValue("CustomsFee", out var customs)) CustomsFee = customs;
                if (defaults.TryGetValue("VatPercent", out var vat)) VatPercent = vat;
                if (defaults.TryGetValue("CorrectionPercent", out var corr)) CorrectionPercent = corr;
                if (defaults.TryGetValue("SecurityPercent", out var sec)) SecurityPercent = sec;
                if (defaults.TryGetValue("DeclarationPercent", out var decl)) DeclarationPercent = decl;
                if (defaults.TryGetValue("CertificationPercent", out var cert)) CertificationPercent = cert;
                if (defaults.TryGetValue("CalculationBase", out var calcBase)) CalculationBase = calcBase;
                if (defaults.TryGetValue("LoadingPercent", out var load)) LoadingPercent = load;
            }
        }
        catch { /* Используем хардкодные дефолты */ }
    }

    [RelayCommand]
    private void ToggleCostParams()
    {
        ShowCostParams = !ShowCostParams;
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
            Note = NewNote,
            Sku = NewProductSku,
            Name = NewProductName
        });
        NewProductId = 0; NewProductSku = null; NewProductName = null; NewQty = 1; NewUnitCost = 0; NewCode = string.Empty; NewNote = null;
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
            if (ok)
            {
                Items.Clear();
                try
                {
                    var select = App.Services.GetRequiredService<ProjectApp.Client.Maui.Views.UserSelectPage>();
                    NavigationHelper.SetRoot(new NavigationPage(select));
                }
                catch { }
            }
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
        TransferProductId = 0; TransferProductSku = null; TransferProductName = null; TransferQty = 1m;
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
            if (ok)
            {
                TransferItems.Clear();
                try
                {
                    var select = App.Services.GetRequiredService<ProjectApp.Client.Maui.Views.UserSelectPage>();
                    NavigationHelper.SetRoot(new NavigationPage(select));
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task PreviewCostAsync()
    {
        if (Items.Count == 0)
        {
            StatusMessage = "Добавьте позиции для расчета";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            HasCostPreview = false;

            var preview = await _supplies.PreviewCostAsync(new SupplyDraft
            {
                Items = Items.ToList(),
                ExchangeRate = ExchangeRate,
                CustomsFee = CustomsFee,
                VatPercent = VatPercent,
                CorrectionPercent = CorrectionPercent,
                SecurityPercent = SecurityPercent,
                DeclarationPercent = DeclarationPercent,
                CertificationPercent = CertificationPercent,
                CalculationBase = CalculationBase,
                LoadingPercent = LoadingPercent
            });

            if (preview != null)
            {
                CostPreviewItems.Clear();
                foreach (var item in preview.Items)
                {
                    CostPreviewItems.Add(new CostPreviewItemVm
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Sku = item.Sku,
                        Quantity = item.Quantity,
                        PriceRub = item.PriceRub,
                        PriceTotal = item.PriceTotal,
                        CustomsAmount = item.CustomsAmount,
                        VatAmount = item.VatAmount,
                        CorrectionAmount = item.CorrectionAmount,
                        SecurityAmount = item.SecurityAmount,
                        DeclarationAmount = item.DeclarationAmount,
                        CertificationAmount = item.CertificationAmount,
                        LoadingAmount = item.LoadingAmount,
                        TotalCost = item.TotalCost,
                        UnitCost = item.UnitCost
                    });
                }
                GrandTotalCost = preview.GrandTotalCost;
                HasCostPreview = true;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка расчета: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public class CostPreviewItemVm
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public decimal Quantity { get; set; }
    public decimal PriceRub { get; set; }
    public decimal PriceTotal { get; set; }
    public decimal CustomsAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal CorrectionAmount { get; set; }
    public decimal SecurityAmount { get; set; }
    public decimal DeclarationAmount { get; set; }
    public decimal CertificationAmount { get; set; }
    public decimal LoadingAmount { get; set; }
    public decimal TotalCost { get; set; }
    public decimal UnitCost { get; set; }
}
