using ProjectApp.Client.Maui.ViewModels;
using ProjectApp.Client.Maui.Services;
using System.Globalization;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectApp.Client.Maui.Views;

public partial class SupplyEditPage : ContentPage
{
    private CancellationTokenSource? _priceDebounceCts;
    private CostingPreviewViewModel? _costingVm;

    public SupplyEditPage(SupplyEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        try
        {
            var sp = App.Current?.Handler?.MauiContext?.Services;
            if (sp != null)
            {
                _costingVm = sp.GetService<CostingPreviewViewModel>();
                if (_costingVm != null)
                {
                    CostingSection.BindingContext = _costingVm;
                }
            }
        }
        catch { }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        try
        {
            System.Diagnostics.Debug.WriteLine("=== SupplyEditPage OnAppearing START");
            
            if (BindingContext == null)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: BindingContext is NULL!");
                await DisplayAlert("Ошибка", "BindingContext is NULL", "ОК");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"BindingContext type: {BindingContext.GetType()}");
            
            if (BindingContext is SupplyEditViewModel vm)
            {
                System.Diagnostics.Debug.WriteLine("BindingContext is SupplyEditViewModel - OK");
                System.Diagnostics.Debug.WriteLine("Calling LoadSupply...");
                
                try
                {
                    await vm.LoadSupply();
                    System.Diagnostics.Debug.WriteLine("LoadSupply completed successfully");
                }
                catch (Exception loadEx)
                {
                    System.Diagnostics.Debug.WriteLine($"LoadSupply error: {loadEx}");
                    System.Diagnostics.Debug.WriteLine($"LoadSupply StackTrace: {loadEx.StackTrace}");
                    await DisplayAlert("Ошибка в LoadSupply", $"{loadEx.Message}\n\n{loadEx.StackTrace}", "ОК");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"ERROR: BindingContext is not SupplyEditViewModel! Type: {BindingContext.GetType()}");
                await DisplayAlert("Ошибка", $"Wrong ViewModel type: {BindingContext.GetType()}", "ОК");
            }
            
            // Инициализация и запуск предпросчёта себестоимости
            try
            {
                if (_costingVm != null && BindingContext is SupplyEditViewModel sVm && sVm.Supply?.Id > 0)
                {
                    _costingVm.SupplyId = sVm.Supply.Id;
                    await _costingVm.RecalculateAsync();
                }
            }
            catch { }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnAppearing outer error: {ex}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            await DisplayAlert("Ошибка OnAppearing", $"{ex.Message}\n\n{ex.StackTrace}", "ОК");
        }
    }

    private async void OnAddProductClicked(object sender, EventArgs e)
    {
        try
        {
            // 1. Ввод НАЗВАНИЯ товара (не ID!)
            var productName = await DisplayPromptAsync(
                "Добавить товар",
                "Введите название товара:",
                "Далее",
                "Отмена",
                placeholder: "Огнетушитель ОП-5");
            
            if (string.IsNullOrWhiteSpace(productName))
                return;
            
            // 2. Ввод SKU (артикула)
            var sku = await DisplayPromptAsync(
                "Добавить товар",
                "Введите артикул (SKU):",
                "Далее",
                "Отмена",
                placeholder: "OP-5-2024");
            
            if (string.IsNullOrWhiteSpace(sku))
                sku = $"AUTO-{DateTime.Now:yyyyMMddHHmmss}"; // Авто-генерация если пустой
            
            // 3. Ввод количества
            var quantityStr = await DisplayPromptAsync(
                "Добавить товар",
                "Введите количество:",
                "Далее",
                "Отмена",
                placeholder: "10",
                keyboard: Keyboard.Numeric);
            
            if (string.IsNullOrWhiteSpace(quantityStr) || !int.TryParse(quantityStr, out int quantity))
                return;
            
            // 4. Ввод цены в рублях
            var priceStr = await DisplayPromptAsync(
                "Добавить товар",
                "Введите цену в рублях:",
                "Далее",
                "Отмена",
                placeholder: "1500.50",
                keyboard: Keyboard.Numeric);
            
            if (string.IsNullOrWhiteSpace(priceStr) || !decimal.TryParse(priceStr, out decimal price))
                return;
            
            // 5. Ввод веса
            var weightStr = await DisplayPromptAsync(
                "Добавить товар",
                "Введите вес (кг):",
                "Далее",
                "Отмена",
                placeholder: "5.5",
                keyboard: Keyboard.Numeric);
            
            if (string.IsNullOrWhiteSpace(weightStr) || !decimal.TryParse(weightStr, out decimal weight))
                return;
            
            // 6. Ввод категории
            var category = await DisplayPromptAsync(
                "Добавить товар",
                "Введите категорию:",
                "Добавить",
                "Отмена",
                placeholder: "Электроника");
            
            if (string.IsNullOrWhiteSpace(category))
                category = "Другое"; // Категория по умолчанию
            
            // 7. Добавляем товар в поставку И сразу в расчёт себестоимости
            if (BindingContext is SupplyEditViewModel vm)
            {
                // ✅ СОХРАНЯЕМ НА СЕРВЕР через API
                var suppliesService = App.Current.Handler.MauiContext.Services.GetService<ISuppliesService>();
                if (suppliesService != null && vm.Supply.Id > 0)
                {
                    await suppliesService.AddSupplyItemAsync(vm.Supply.Id, productName, quantity, price, category, sku, weight);
                    System.Diagnostics.Debug.WriteLine($"✅ Товар сохранен на сервер с категорией: {category}");
                    
                    // Перезагружаем список товаров с сервера
                    await vm.LoadSupply();
                }
                
                // ✅ Добавляем в расчёт себестоимости
                try
                {
                    var batchCostService = App.Current.Handler.MauiContext.Services.GetService<IBatchCostService>();
                    if (batchCostService != null && vm.Supply.Id > 0)
                    {
                        await batchCostService.AddItemAsync(vm.Supply.Id, productName, quantity, price);
                        System.Diagnostics.Debug.WriteLine($"✅ Товар добавлен в расчёт себестоимости");
                    }
                }
                catch (Exception costEx)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Не удалось добавить в расчёт себеса: {costEx.Message}");
                }
                
                await DisplayAlert("✅ Добавлено", 
                    $"Товар \"{productName}\" ({category}) добавлен в поставку и в расчёт себестоимости", 
                    "ОК");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnAddProductClicked error: {ex}");
            await DisplayAlert("Ошибка", $"Не удалось добавить товар: {ex.Message}", "ОК");
        }
    }

    private async void OnRemoveProductClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter != null)
        {
            var confirm = await DisplayAlert("Удалить товар?", "Вы уверены?", "Да", "Нет");
            if (confirm && BindingContext is SupplyEditViewModel vm)
            {
                await vm.RemoveProduct(button.CommandParameter);
            }
        }
    }

    private void OnInlineNumberChanged(object sender, TextChangedEventArgs e)
    {
        if (BindingContext is SupplyEditViewModel vm)
        {
            vm.TriggerLocalRecalculate();
        }
    }

    private async void OnPriceRubChanged(object sender, TextChangedEventArgs e)
    {
        _priceDebounceCts?.Cancel();
        _priceDebounceCts?.Dispose();
        _priceDebounceCts = new CancellationTokenSource();
        var token = _priceDebounceCts.Token;

        try
        {
            await Task.Delay(200, token);

            if (token.IsCancellationRequested)
                return;

            if (sender is Entry entry && entry.BindingContext is BatchCostItemDto item)
            {
                var text = entry.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    text = text.Replace(',', '.');
                    if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                    {
                        if (item.PriceRub != value)
                        {
                            item.PriceRub = value;
                        }
                    }
                }

                if (BindingContext is SupplyEditViewModel vm)
                {
                    vm.RecalculateSingleItem(item);
                }
            }
        }
        catch (TaskCanceledException)
        {
            // debounced
        }
    }

    private async void OnRecalculateClicked(object sender, EventArgs e)
    {
        if (_costingVm != null)
        {
            await _costingVm.RecalculateAsync();
        }
    }
}
