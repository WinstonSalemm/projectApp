using ProjectApp.Client.Maui.ViewModels;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.Views;

public partial class SupplyEditPage : ContentPage
{
    public SupplyEditPage(SupplyEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
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
                "Добавить",
                "Отмена",
                placeholder: "5.5",
                keyboard: Keyboard.Numeric);
            
            if (string.IsNullOrWhiteSpace(weightStr) || !decimal.TryParse(weightStr, out decimal weight))
                return;
            
            // 6. Добавляем товар в поставку И сразу в расчёт себестоимости
            if (BindingContext is SupplyEditViewModel vm)
            {
                var newItem = new SupplyItemDto
                {
                    ProductId = 0, // 0 = новый товар, backend назначит реальный ID
                    ProductName = productName,
                    Sku = sku,
                    Quantity = quantity,
                    PriceRub = price,
                    Weight = weight
                };
                
                vm.SupplyItems.Add(newItem);
                
                // ✅ АВТОМАТИЧЕСКИ добавляем в расчёт себестоимости
                try
                {
                    var batchCostService = App.Current.Handler.MauiContext.Services.GetService<IBatchCostService>();
                    if (batchCostService != null && vm.Supply.Id > 0)
                    {
                        await batchCostService.AddItemAsync(vm.Supply.Id, productName, quantity, price);
                        System.Diagnostics.Debug.WriteLine($"✅ Товар автоматически добавлен в расчёт себестоимости");
                    }
                }
                catch (Exception costEx)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Не удалось добавить в расчёт себеса: {costEx.Message}");
                    // Не показываем ошибку пользователю, товар уже добавлен в поставку
                }
                
                await DisplayAlert("✅ Добавлено", 
                    $"Товар \"{productName}\" добавлен в поставку и в расчёт себестоимости", 
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

    private async void OnCostCalculationClicked(object sender, EventArgs e)
    {
        try
        {
            if (BindingContext is SupplyEditViewModel vm && vm.Supply != null)
            {
                var costPage = App.Current.Handler.MauiContext.Services.GetService<BatchCostCalculationPage>();
                
                if (costPage != null && costPage.BindingContext is BatchCostCalculationViewModel costVm)
                {
                    var queryParams = new Dictionary<string, object>
                    {
                        ["supplyId"] = vm.Supply.Id.ToString(),
                        ["supplyCode"] = vm.Supply.Code
                    };
                    costVm.ApplyQueryAttributes(queryParams);
                    
                    await Navigation.PushAsync(costPage);
                }
                else
                {
                    await DisplayAlert("Ошибка", "Не удалось открыть страницу расчета", "ОК");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnCostCalculationClicked error: {ex}");
            await DisplayAlert("Ошибка", $"Не удалось открыть расчет: {ex.Message}", "ОК");
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (BindingContext is SupplyEditViewModel vm)
        {
            await vm.Save();
        }
    }
}
