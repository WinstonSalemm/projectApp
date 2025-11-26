using ProjectApp.Client.Maui.ViewModels;
using ProjectApp.Client.Maui.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Maui.ApplicationModel;
using System.Linq;
#if WINDOWS
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
#endif

namespace ProjectApp.Client.Maui.Views;

public partial class SupplyEditPage : ContentPage
{
    private CostingPreviewViewModel? _costingVm;
    public CostingPreviewViewModel? CostingVm
    {
        get => _costingVm;
        private set
        {
            if (_costingVm == value) return;
            _costingVm = value;
            OnPropertyChanged(nameof(CostingVm));
        }
    }

    public SupplyEditPage(SupplyEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        // подтянем VM для предпросмотра
        var sp = App.Current?.Handler?.MauiContext?.Services;
        if (CostingVm == null && sp != null)
            CostingVm = sp.GetService<CostingPreviewViewModel>();

        if (CostingVm != null)
            CostingSection.BindingContext = CostingVm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            if (BindingContext is SupplyEditViewModel vm)
                await vm.LoadSupply();

            if (CostingVm != null && BindingContext is SupplyEditViewModel sVm && sVm.Supply?.Id > 0)
            {
                // гарантируем биндинг секции на VM расчёта
                CostingSection.BindingContext = CostingVm;
                CostingVm.SupplyId = sVm.Supply.Id;
                await CostingVm.RecalculateAsync();

                // построим локальную проекцию и итоги
                RebuildDisplayedRows();

                await Task.Delay(50);
                if (TableScroll is not null)
                    await TableScroll.ScrollToAsync(0, 0, false);

#if WINDOWS
                // enable horizontal scrolling by mouse wheel on Windows (UI-only behavior)
                TryEnableHorizontalWheel();
#endif
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }

#if WINDOWS
    private bool _wheelHooked;
    private void TryEnableHorizontalWheel()
    {
        if (_wheelHooked) return;
        var platformView = TableScroll?.Handler?.PlatformView as ScrollViewer;
        if (platformView is null) return;

        _wheelHooked = true;
        platformView.PointerWheelChanged += OnScrollViewerPointerWheelChanged;
    }

    private void OnScrollViewerPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        var point = e.GetCurrentPoint(sv);
        var delta = point.Properties.MouseWheelDelta; // positive: wheel up
        const double step = 80; // px per notch

        var newOffset = sv.HorizontalOffset - Math.Sign(delta) * step;
        if (newOffset < 0) newOffset = 0;
        sv.ChangeView(newOffset, null, null);
        e.Handled = true;
    }
#endif

    private async void OnAddProductClicked(object sender, EventArgs e)
    {
        // оставляю как было у тебя: добавление товара -> загрузка -> пересчёт -> перерисовка таблицы
        if (BindingContext is not SupplyEditViewModel vm) return;

        var name = await DisplayPromptAsync("Добавить товар", "Название:", "OK", "Отмена", "Огнетушитель ОП-5");
        if (string.IsNullOrWhiteSpace(name)) return;

        var sku = await DisplayPromptAsync("Добавить товар", "SKU:", "OK", "Отмена", "OP-5");
        if (string.IsNullOrWhiteSpace(sku)) sku = $"AUTO-{DateTime.Now:yyyyMMddHHmmss}";

        var quantityStr = await DisplayPromptAsync(
            "Добавить товар",
            "Введите количество:",
            "Далее",
            "Отмена",
            placeholder: "10",
            maxLength: -1,
            keyboard: Keyboard.Numeric);
        if (!int.TryParse(quantityStr, out var qty)) return;

        var priceText = await DisplayPromptAsync(
            "Добавить товар",
            "Цена (₽):",
            "OK",
            "Отмена",
            placeholder: "500",
            maxLength: -1,
            keyboard: Keyboard.Numeric);
        if (!decimal.TryParse(N(priceText), NumberStyles.Any, CultureInfo.InvariantCulture, out var priceRub)) return;

        var weightStr = await DisplayPromptAsync(
            "Добавить товар",
            "Введите вес (кг):",
            "Далее",
            "Отмена",
            placeholder: "5.5",
            maxLength: -1,
            keyboard: Keyboard.Numeric);
        if (!decimal.TryParse(N(weightStr), NumberStyles.Any, CultureInfo.InvariantCulture, out var weight)) return;

        string? category = null;

        // Сначала пробуем предложить существующие категории через рулетку (ActionSheet)
        try
        {
            var sp = App.Services;
            var catalog = sp?.GetService<ICatalogService>();
            if (catalog != null)
            {
                var cats = (await catalog.GetCategoriesAsync()).ToList();
                if (cats.Count > 0)
                {
                    const string newCategoryOption = "Новая категория...";
                    var actionItems = new List<string>(cats)
                    {
                        newCategoryOption
                    };

                    var picked = await DisplayActionSheet("Категория", "Отмена", null, actionItems.ToArray());
                    if (string.IsNullOrWhiteSpace(picked) || picked == "Отмена")
                        return;

                    if (picked == newCategoryOption)
                    {
                        category = await DisplayPromptAsync("Добавить товар", "Категория:", "OK", "Отмена", "Пожарное оборудование");
                        if (string.IsNullOrWhiteSpace(category)) return;
                    }
                    else
                    {
                        category = picked;
                    }
                }
            }
        }
        catch
        {
            // Если что-то пошло не так с загрузкой категорий - просто переходим к ручному вводу ниже
        }

        // Если категории так и не выбрали из списка, используем старый вариант с ручным вводом
        if (string.IsNullOrWhiteSpace(category))
        {
            category = await DisplayPromptAsync("Добавить товар", "Категория:", "OK", "Отмена", "Пожарное оборудование");
            if (string.IsNullOrWhiteSpace(category)) return;
        }

        var supplies = App.Current?.Handler?.MauiContext?.Services?.GetService<ISuppliesService>();
        if (supplies != null && vm.Supply.Id > 0)
        {
            await supplies.AddSupplyItemAsync(vm.Supply.Id, name, qty, priceRub, category, sku, weight);
            await vm.LoadSupply();
        }

        if (CostingVm != null)
        {
            await CostingVm.RecalculateAsync();
            RebuildDisplayedRows();
        }
    }

    private async void OnRemoveProductClicked(object sender, EventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.Button b || BindingContext is not SupplyEditViewModel vm) return;
        if (!await DisplayAlert("Удалить товар?", "Вы уверены?", "Да", "Нет")) return;

        var param = b.CommandParameter;
        if (param is DisplayedRow dr)
        {
            if (dr.SupplyItemIndex >= 0 && dr.SupplyItemIndex < vm.SupplyItems.Count)
            {
                param = vm.SupplyItems[dr.SupplyItemIndex];
            }
            else
            {
                var key = SNorm(dr.SkuOrName);
                var match = vm.SupplyItems.FirstOrDefault(si => SNorm(si.Sku) == key || SNorm(si.Name) == key)
                           ?? vm.SupplyItems.FirstOrDefault(si => SNorm(si.Name).Contains(key) || key.Contains(SNorm(si.Name)));
                if (match != null)
                    param = match;
                else
                {
                    await DisplayAlert("Ошибка", "Не удалось сопоставить позицию для удаления", "ОК");
                    return;
                }
            }
        }

        await vm.RemoveProduct(param);
        if (CostingVm != null)
        {
            await vm.LoadSupply();
            await CostingVm.RecalculateAsync();
            RebuildDisplayedRows();
        }
    }

    private async void OnRecalculateClicked(object? sender, EventArgs e)
    {
        if (CostingVm == null) return;

        // общий курс (из Excel-поля) — только подставляем в существующие свойства, БЕЗ смены твоей логики
        if (!string.IsNullOrWhiteSpace(AnyFxEntry?.Text) &&
            decimal.TryParse(N(AnyFxEntry.Text), NumberStyles.Any, CultureInfo.InvariantCulture, out var fx))
        {
            CostingVm.RubToUzs = fx;
            CostingVm.UsdToUzs = fx;
        }

        await CostingVm.RecalculateAsync();
        RebuildDisplayedRows();
        await Task.Delay(30);
        await TableScroll.ScrollToAsync(0,0,false);
    }

    // ===== helpers =====
    private static string N(string? s) => (s ?? "").Trim().Replace(" ", string.Empty).Replace(',', '.');
    private static string SNorm(string? s) => (s ?? "").Trim().ToLowerInvariant();

    // ===== Local visual layer (DisplayedRows) =====
    public ObservableCollection<DisplayedRow> DisplayedRows { get; } = new();

    public decimal TotalQtyDisplay { get; private set; }
    public decimal TotalBaseSumUzsDisplay { get; private set; }
    public decimal TotalCostUzsDisplay { get; private set; }
    public decimal WeightedCostPerUnitUzsDisplay => TotalQtyDisplay > 0 ? Math.Round(TotalCostUzsDisplay / TotalQtyDisplay, 2, MidpointRounding.AwayFromZero) : 0m;

    private CancellationTokenSource? _debouncePriceCts;
    private DisplayedRow? _pendingPriceRow;

    private void RebuildDisplayedRows()
    {
        if (CostingVm == null) return;

        var fx = CostingVm.RubToUzs;

        var totalQty = CostingVm.Rows.Sum(r => r.Quantity);
        if (totalQty <= 0) totalQty = 1;
        var totalBaseSum = CostingVm.Rows.Sum(r => r.LineBaseTotalUzs);
        if (totalBaseSum <= 0) totalBaseSum = 1;

        var fee10Party = ParseUzs(CustomsFee10Entry?.Text);
        var fee12Party = 0m;
        var loadingParty = ParseUzs(LoadingPartyEntry?.Text);
        var logisticsParty = ParseUzs(LogisticsPartyEntry?.Text);

        var fee12PerUnit = fee12Party / totalQty; // reserved, not used now

        var previous = DisplayedRows.ToDictionary(x => x.SkuOrName, x => x.PriceRub);
        DisplayedRows.Clear();
        var svm = BindingContext as SupplyEditViewModel;
        foreach (var r in CostingVm.Rows)
        {
            // Try keep previous RUB price if exists
            var priceRub = previous.TryGetValue(r.SkuOrName, out var prevRub)
                ? prevRub
                : (fx > 0 ? Math.Round(r.BasePriceUzs / fx, 2, MidpointRounding.AwayFromZero) : 0m);

            var dr = new DisplayedRow
            {
                RowNo = r.RowNo,
                SkuOrName = r.SkuOrName,
                Quantity = r.Quantity,
                PriceRub = priceRub,
                VmCustomsUzsPerUnit = r.CustomsUzsPerUnit,
                Fee12UzsPerUnit = fee12PerUnit,
            };

            dr.WeightShare = r.LineBaseTotalUzs / totalBaseSum;
            var qty = dr.Quantity > 0 ? dr.Quantity : 1m;
            dr.Fee10UzsPerUnit = (fee10Party * dr.WeightShare) / qty;
            dr.LoadingUzsPerUnit = (loadingParty * dr.WeightShare) / qty;
            dr.LogisticsUzsPerUnit = (logisticsParty * dr.WeightShare) / qty;

            var idx = -1;
            if (svm != null && svm.SupplyItems.Count > 0)
            {
                var key = SNorm(r.SkuOrName);
                for (int i = 0; i < svm.SupplyItems.Count; i++)
                {
                    var si = svm.SupplyItems[i];
                    if (SNorm(si.Name) == key || SNorm(si.Sku) == key) { idx = i; break; }
                }
                if (idx < 0)
                {
                    for (int i = 0; i < svm.SupplyItems.Count; i++)
                    {
                        var si = svm.SupplyItems[i];
                        var nm = SNorm(si.Name);
                        if (nm.Contains(key) || key.Contains(nm)) { idx = i; break; }
                    }
                }
            }
            dr.SupplyItemIndex = idx;

            RecalculateRow(dr, fx,
                CostingVm.VatPct, CostingVm.LogisticsPct, CostingVm.WarehousePct,
                CostingVm.DeclarationPct, CostingVm.CertificationPct, CostingVm.McsPct,
                CostingVm.DeviationPct);

            DisplayedRows.Add(dr);
        }

        RecalculateTotals();
        OnPropertyChanged(nameof(DisplayedRows));
        OnPropertyChanged(nameof(WeightedCostPerUnitUzsDisplay));
    }

    private void RecalculateTotals()
    {
        TotalQtyDisplay = DisplayedRows.Sum(x => x.Quantity);
        TotalBaseSumUzsDisplay = DisplayedRows.Sum(x => x.LineBaseTotalUzs);
        TotalCostUzsDisplay = DisplayedRows.Sum(x => x.LineCostUzs);
        OnPropertyChanged(nameof(TotalQtyDisplay));
        OnPropertyChanged(nameof(TotalBaseSumUzsDisplay));
        OnPropertyChanged(nameof(TotalCostUzsDisplay));
        OnPropertyChanged(nameof(WeightedCostPerUnitUzsDisplay));
    }

    private void RecalculateRow(DisplayedRow dr, decimal fx,
        decimal vatPct, decimal logisticsPct, decimal warehousePct,
        decimal declarationPct, decimal certificationPct, decimal mcsPct,
        decimal deviationPct)
    {
        var baseUzsUnit = Math.Round(dr.PriceRub * fx, 2, MidpointRounding.AwayFromZero);
        dr.BasePriceUzs = baseUzsUnit;

        var vatUzs = 0m;
        var logisticsUzs = dr.LogisticsUzsPerUnit;
        var wareUzs = baseUzsUnit * warehousePct;
        var declUzs = baseUzsUnit * declarationPct;
        var certUzs = baseUzsUnit * certificationPct;
        var mcsUzs = baseUzsUnit * mcsPct;
        var devUzs = baseUzsUnit * deviationPct;

        var customsUzsPerUnit = (dr.VmCustomsUzsPerUnit) + dr.Fee10UzsPerUnit + dr.Fee12UzsPerUnit;

        dr.CustomsUzsPerUnit = customsUzsPerUnit;
        dr.VatUzsPerUnit = vatUzs;
        dr.LogisticsUzsPerUnit = logisticsUzs;
        dr.WarehouseUzsPerUnit = wareUzs;
        dr.DeclarationUzsPerUnit = declUzs;
        dr.CertificationUzsPerUnit = certUzs;
        dr.McsUzsPerUnit = mcsUzs;
        dr.DeviationUzsPerUnit = devUzs;

        var costPerUnit = baseUzsUnit
                          + customsUzsPerUnit
                          + dr.LoadingUzsPerUnit
                          + vatUzs + logisticsUzs + wareUzs + declUzs
                          + certUzs + mcsUzs + devUzs;

        dr.CostPerUnitUzs = costPerUnit;
        dr.LineCostUzs = costPerUnit * dr.Quantity;
        dr.LineBaseTotalUzs = baseUzsUnit * dr.Quantity;

        // Percent hint: logistics as % of base
        dr.PercentHint = baseUzsUnit > 0 ? Math.Round((logisticsUzs / baseUzsUnit) * 100m, 0, MidpointRounding.AwayFromZero) : 0m;
    }

    private static decimal ParseUzs(string? s)
        => decimal.TryParse(N(s), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;

    private void OnPriceRubChanged(object? sender, Microsoft.Maui.Controls.TextChangedEventArgs e)
    {
        if (sender is not Entry entry) return;
        if (entry.BindingContext is not DisplayedRow row) return;

        _pendingPriceRow = row;
        _debouncePriceCts?.Cancel();
        _debouncePriceCts?.Dispose();
        _debouncePriceCts = new CancellationTokenSource();
        var token = _debouncePriceCts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(200, token);
                if (token.IsCancellationRequested) return;

                if (decimal.TryParse(N(entry.Text), NumberStyles.Any, CultureInfo.InvariantCulture, out var pr))
                {
                    row.PriceRub = pr;
                    var fx = CostingVm?.RubToUzs ?? 0m;
                    if (fx <= 0 && !string.IsNullOrWhiteSpace(AnyFxEntry?.Text) && decimal.TryParse(N(AnyFxEntry.Text), NumberStyles.Any, CultureInfo.InvariantCulture, out var manualFx))
                        fx = manualFx;

                    RecalculateRow(row, fx,
                        CostingVm?.VatPct ?? 0m, CostingVm?.LogisticsPct ?? 0m, CostingVm?.WarehousePct ?? 0m,
                        CostingVm?.DeclarationPct ?? 0m, CostingVm?.CertificationPct ?? 0m, CostingVm?.McsPct ?? 0m,
                        CostingVm?.DeviationPct ?? 0m);

                    await MainThread.InvokeOnMainThreadAsync(RecalculateTotals);
                }
            }
            catch (TaskCanceledException)
            {
                // ignored
            }
        }, token);
    }

    private void OnQuantityChanged(object? sender, Microsoft.Maui.Controls.TextChangedEventArgs e)
    {
        if (sender is not Entry entry) return;
        if (entry.BindingContext is not DisplayedRow row) return;

        if (decimal.TryParse(N(entry.Text), NumberStyles.Any, CultureInfo.InvariantCulture, out var qty))
        {
            if (qty < 0) qty = 0;
            row.Quantity = qty;
            RecalculateAllDisplayedRows();
        }
    }

    private void RecalculateAllDisplayedRows()
    {
        if (CostingVm == null) { RecalculateTotals(); return; }

        var fx = CostingVm.RubToUzs;
        if (fx <= 0 && !string.IsNullOrWhiteSpace(AnyFxEntry?.Text) && decimal.TryParse(N(AnyFxEntry.Text), NumberStyles.Any, CultureInfo.InvariantCulture, out var manualFx))
            fx = manualFx;

        var fee10Party = ParseUzs(CustomsFee10Entry?.Text);
        var loadingParty = ParseUzs(LoadingPartyEntry?.Text);
        var logisticsParty = ParseUzs(LogisticsPartyEntry?.Text);

        // total base sum across displayed rows using current quantities
        decimal TotalBaseForAll()
        {
            decimal sum = 0;
            foreach (var dr in DisplayedRows)
            {
                var unitBase = dr.BasePriceUzs > 0 ? dr.BasePriceUzs : Math.Round(dr.PriceRub * fx, 2, MidpointRounding.AwayFromZero);
                sum += unitBase * (dr.Quantity > 0 ? dr.Quantity : 1);
            }
            return sum > 0 ? sum : 1;
        }

        var totalBase = TotalBaseForAll();

        foreach (var dr in DisplayedRows)
        {
            var unitBase = dr.BasePriceUzs > 0 ? dr.BasePriceUzs : Math.Round(dr.PriceRub * fx, 2, MidpointRounding.AwayFromZero);
            dr.BasePriceUzs = unitBase;
            dr.WeightShare = (unitBase * (dr.Quantity > 0 ? dr.Quantity : 1)) / totalBase;
            var qty = dr.Quantity > 0 ? dr.Quantity : 1m;
            dr.Fee10UzsPerUnit = (fee10Party * dr.WeightShare) / qty;
            dr.LoadingUzsPerUnit = (loadingParty * dr.WeightShare) / qty;
            dr.LogisticsUzsPerUnit = (logisticsParty * dr.WeightShare) / qty;

            RecalculateRow(dr, fx,
                CostingVm.VatPct, CostingVm.LogisticsPct, CostingVm.WarehousePct,
                CostingVm.DeclarationPct, CostingVm.CertificationPct, CostingVm.McsPct,
                CostingVm.DeviationPct);
        }

        RecalculateTotals();
    }

    public sealed class DisplayedRow : INotifyPropertyChanged
    {
        private decimal _priceRub;
        private decimal _basePriceUzs;
        private decimal _customsUzsPerUnit;
        private decimal _loadingUzsPerUnit;
        private decimal _vatUzsPerUnit;
        private decimal _logisticsUzsPerUnit;
        private decimal _warehouseUzsPerUnit;
        private decimal _declarationUzsPerUnit;
        private decimal _certificationUzsPerUnit;
        private decimal _mcsUzsPerUnit;
        private decimal _deviationUzsPerUnit;
        private decimal _costPerUnitUzs;
        private decimal _lineCostUzs;
        private decimal _percentHint;
        private decimal _lineBaseTotalUzs;
        private decimal _weightShare;

        public int RowNo { get; set; }
        public string SkuOrName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public int SupplyItemIndex { get; set; } = -1;
        public decimal WeightShare { get => _weightShare; set { if (_weightShare != value) { _weightShare = value; OnPropertyChanged(); } } }

        public decimal PriceRub { get => _priceRub; set { if (_priceRub != value) { _priceRub = value; OnPropertyChanged(); } } }

        // Base UZS per unit (maps to "BasePriceUzs" binding in XAML)
        public decimal BasePriceUzs { get => _basePriceUzs; set { if (_basePriceUzs != value) { _basePriceUzs = value; OnPropertyChanged(); } } }

        // From VM row
        public decimal VmCustomsUzsPerUnit { get; set; }

        // Party-distributed absolutes per unit
        public decimal Fee10UzsPerUnit { get; set; }
        public decimal Fee12UzsPerUnit { get; set; }
        public decimal LoadingUzsPerUnit { get => _loadingUzsPerUnit; set { if (_loadingUzsPerUnit != value) { _loadingUzsPerUnit = value; OnPropertyChanged(); } } }

        // Resulting per-unit columns used by existing XAML bindings
        public decimal CustomsUzsPerUnit { get => _customsUzsPerUnit; set { if (_customsUzsPerUnit != value) { _customsUzsPerUnit = value; OnPropertyChanged(); } } }
        public decimal VatUzsPerUnit { get => _vatUzsPerUnit; set { if (_vatUzsPerUnit != value) { _vatUzsPerUnit = value; OnPropertyChanged(); } } }
        public decimal LogisticsUzsPerUnit { get => _logisticsUzsPerUnit; set { if (_logisticsUzsPerUnit != value) { _logisticsUzsPerUnit = value; OnPropertyChanged(); } } }
        public decimal WarehouseUzsPerUnit { get => _warehouseUzsPerUnit; set { if (_warehouseUzsPerUnit != value) { _warehouseUzsPerUnit = value; OnPropertyChanged(); } } }
        public decimal DeclarationUzsPerUnit { get => _declarationUzsPerUnit; set { if (_declarationUzsPerUnit != value) { _declarationUzsPerUnit = value; OnPropertyChanged(); } } }
        public decimal CertificationUzsPerUnit { get => _certificationUzsPerUnit; set { if (_certificationUzsPerUnit != value) { _certificationUzsPerUnit = value; OnPropertyChanged(); } } }
        public decimal McsUzsPerUnit { get => _mcsUzsPerUnit; set { if (_mcsUzsPerUnit != value) { _mcsUzsPerUnit = value; OnPropertyChanged(); } } }
        public decimal DeviationUzsPerUnit { get => _deviationUzsPerUnit; set { if (_deviationUzsPerUnit != value) { _deviationUzsPerUnit = value; OnPropertyChanged(); } } }

        public decimal CostPerUnitUzs { get => _costPerUnitUzs; set { if (_costPerUnitUzs != value) { _costPerUnitUzs = value; OnPropertyChanged(); } } }
        public decimal LineCostUzs { get => _lineCostUzs; set { if (_lineCostUzs != value) { _lineCostUzs = value; OnPropertyChanged(); } } }

        // Extra visuals
        public decimal PercentHint { get => _percentHint; set { if (_percentHint != value) { _percentHint = value; OnPropertyChanged(); } } }
        public decimal LineBaseTotalUzs { get => _lineBaseTotalUzs; set { if (_lineBaseTotalUzs != value) { _lineBaseTotalUzs = value; OnPropertyChanged(); } } }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
