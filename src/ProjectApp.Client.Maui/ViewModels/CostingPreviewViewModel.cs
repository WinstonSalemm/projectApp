using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;
using Microsoft.Maui.ApplicationModel;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class CostingPreviewViewModel : ObservableObject
{
    private readonly ApiCostingPreviewService _api;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private int supplyId;

    // Параметры
    [ObservableProperty] private decimal rubToUzs = 156m;
    [ObservableProperty] private decimal usdToUzs = 12800m;
    [ObservableProperty] private decimal customsFixedUzs = 0m;
    [ObservableProperty] private decimal loadingTotalUzs = 0m;
    [ObservableProperty] private decimal logisticsPct = 0.05m;
    [ObservableProperty] private decimal warehousePct = 0.005m;
    [ObservableProperty] private decimal declarationPct = 0.002m;
    [ObservableProperty] private decimal certificationPct = 0.01m;
    [ObservableProperty] private decimal mcsPct = 0.01m;
    [ObservableProperty] private decimal deviationPct = 0.015m;
    [ObservableProperty] private decimal tradeMarkupPct = 0.15m;
    [ObservableProperty] private decimal vatPct = 0.16m;
    [ObservableProperty] private decimal profitTaxPct = 0.15m;

    // Итоги
    [ObservableProperty] private decimal totalQty;
    [ObservableProperty] private decimal totalBaseSumUzs;
    [ObservableProperty] private decimal totalCostUzs;

    public ObservableCollection<CostingRowVM> Rows { get; } = new();

    private CancellationTokenSource? _debounceCts;
    private readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(300);

    public CostingPreviewViewModel(ApiCostingPreviewService api)
    {
        _api = api;
    }

    [RelayCommand]
    public async Task RecalculateAsync()
    {
        if (SupplyId <= 0) return;
        try
        {
            IsBusy = true;
            var cfg = new CostingConfigDto
            {
                RubToUzs = RubToUzs,
                UsdToUzs = UsdToUzs,
                CustomsFixedUzs = CustomsFixedUzs,
                LoadingTotalUzs = LoadingTotalUzs,
                LogisticsPct = LogisticsPct,
                WarehousePct = WarehousePct,
                DeclarationPct = DeclarationPct,
                CertificationPct = CertificationPct,
                McsPct = McsPct,
                DeviationPct = DeviationPct,
                TradeMarkupPct = TradeMarkupPct,
                VatPct = VatPct,
                ProfitTaxPct = ProfitTaxPct
            };
            var dto = await _api.PreviewAsync(SupplyId, cfg);
            Rows.Clear();
            foreach (var r in dto.Rows)
            {
                Rows.Add(new CostingRowVM(r));
            }
            TotalQty = dto.TotalQty;
            TotalBaseSumUzs = dto.TotalBaseSumUzs;
            TotalCostUzs = Rows.Sum(x => x.CostPerUnitUzs * x.Quantity);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Debounce()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_debounce, token);
                if (!token.IsCancellationRequested)
                    await MainThread.InvokeOnMainThreadAsync(async () => await RecalculateAsync());
            }
            catch (TaskCanceledException) { }
        }, token);
    }

    partial void OnRubToUzsChanged(decimal value) => Debounce();
    partial void OnUsdToUzsChanged(decimal value) => Debounce();
    partial void OnCustomsFixedUzsChanged(decimal value) => Debounce();
    partial void OnLoadingTotalUzsChanged(decimal value) => Debounce();
    partial void OnLogisticsPctChanged(decimal value) => Debounce();
    partial void OnWarehousePctChanged(decimal value) => Debounce();
    partial void OnDeclarationPctChanged(decimal value) => Debounce();
    partial void OnCertificationPctChanged(decimal value) => Debounce();
    partial void OnMcsPctChanged(decimal value) => Debounce();
    partial void OnDeviationPctChanged(decimal value) => Debounce();
    partial void OnTradeMarkupPctChanged(decimal value) => Debounce();
    partial void OnVatPctChanged(decimal value) => Debounce();
    partial void OnProfitTaxPctChanged(decimal value) => Debounce();

    public sealed class CostingRowVM
    {
        public CostingRowVM(CostingRowDto d)
        {
            SkuOrName = d.SkuOrName;
            Quantity = d.Quantity;
            BasePriceUzs = d.BasePriceUzs;
            LineBaseTotalUzs = d.LineBaseTotalUzs;
            CustomsUzsPerUnit = d.CustomsUzsPerUnit;
            LoadingUzsPerUnit = d.LoadingUzsPerUnit;
            LogisticsUzsPerUnit = d.LogisticsUzsPerUnit;
            WarehouseUzsPerUnit = d.WarehouseUzsPerUnit;
            DeclarationUzsPerUnit = d.DeclarationUzsPerUnit;
            CertificationUzsPerUnit = d.CertificationUzsPerUnit;
            McsUzsPerUnit = d.McsUzsPerUnit;
            DeviationUzsPerUnit = d.DeviationUzsPerUnit;
            CostPerUnitUzs = d.CostPerUnitUzs;
            TradePriceUzs = d.TradePriceUzs;
            VatUzs = d.VatUzs;
            PriceWithVatUzs = d.PriceWithVatUzs;
            ProfitPerUnitUzs = d.ProfitPerUnitUzs;
            ProfitTaxUzs = d.ProfitTaxUzs;
            NetProfitUzs = d.NetProfitUzs;

            // Computed line totals (per position)
            LineCostUzs = CostPerUnitUzs * Quantity;
            LineTradePriceUzs = TradePriceUzs * Quantity;
            LineVatUzs = VatUzs * Quantity;
            LinePriceWithVatUzs = PriceWithVatUzs * Quantity;
            LineNetProfitUzs = NetProfitUzs * Quantity;
        }
        public string SkuOrName { get; }
        public decimal Quantity { get; }
        public decimal BasePriceUzs { get; }
        public decimal LineBaseTotalUzs { get; }
        public decimal CustomsUzsPerUnit { get; }
        public decimal LoadingUzsPerUnit { get; }
        public decimal LogisticsUzsPerUnit { get; }
        public decimal WarehouseUzsPerUnit { get; }
        public decimal DeclarationUzsPerUnit { get; }
        public decimal CertificationUzsPerUnit { get; }
        public decimal McsUzsPerUnit { get; }
        public decimal DeviationUzsPerUnit { get; }
        public decimal CostPerUnitUzs { get; }
        public decimal TradePriceUzs { get; }
        public decimal VatUzs { get; }
        public decimal PriceWithVatUzs { get; }
        public decimal ProfitPerUnitUzs { get; }
        public decimal ProfitTaxUzs { get; }
        public decimal NetProfitUzs { get; }

        // Totals per position (Quantity * per-unit value)
        public decimal LineCostUzs { get; }
        public decimal LineTradePriceUzs { get; }
        public decimal LineVatUzs { get; }
        public decimal LinePriceWithVatUzs { get; }
        public decimal LineNetProfitUzs { get; }
    }
}
