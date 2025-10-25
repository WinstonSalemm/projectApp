using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class BatchCostCalculationPage : ContentPage
{
    public BatchCostCalculationPage(BatchCostCalculationViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is BatchCostCalculationViewModel vm)
        {
            await vm.LoadDataAsync();
        }
    }

    private async void OnAddItemClicked(object sender, EventArgs e)
    {
        try
        {
            // 1. Название товара
            var productName = await DisplayPromptAsync(
                "Добавить товар",
                "Введите название товара:",
                "Далее",
                "Отмена",
                placeholder: "Огнетушитель ОП-5");
            
            if (string.IsNullOrWhiteSpace(productName))
                return;
            
            // 2. Количество
            var quantityStr = await DisplayPromptAsync(
                "Добавить товар",
                "Введите количество:",
                "Далее",
                "Отмена",
                placeholder: "10",
                keyboard: Keyboard.Numeric);
            
            if (string.IsNullOrWhiteSpace(quantityStr) || !int.TryParse(quantityStr, out int quantity))
                return;
            
            // 3. Цена в рублях
            var priceStr = await DisplayPromptAsync(
                "Добавить товар",
                "Введите цену в рублях:",
                "Добавить",
                "Отмена",
                placeholder: "1500.50",
                keyboard: Keyboard.Numeric);
            
            if (string.IsNullOrWhiteSpace(priceStr) || !decimal.TryParse(priceStr, out decimal price))
                return;
            
            // Добавляем товар
            if (BindingContext is BatchCostCalculationViewModel vm)
            {
                await vm.AddItemAsync(productName, quantity, price);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", $"Не удалось добавить товар: {ex.Message}", "ОК");
        }
    }

    private async void OnEditItemClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is BatchCostItemDto item)
        {
            await DisplayAlert("Редактирование", "Функция в разработке", "ОК");
        }
    }

    private async void OnDeleteItemClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is BatchCostItemDto item)
        {
            var confirm = await DisplayAlert(
                "Удалить товар?",
                $"Удалить '{item.ProductName}' из расчета?",
                "Да",
                "Нет");
            
            if (confirm && BindingContext is BatchCostCalculationViewModel vm)
            {
                await vm.DeleteItemAsync(item.Id);
            }
        }
    }

    private async void OnViewDetailsClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is BatchCostItemDto item)
        {
            var details = $"Наименование: {item.ProductName}\n" +
                         $"Количество: {item.Quantity}\n" +
                         $"Цена в рублях: {item.PriceRub:F2}\n" +
                         $"Курс: {item.ExchangeRate:F2}\n" +
                         $"Цена в сумах: {item.PriceSom:N0}\n\n" +
                         $"--- Статьи расходов ---\n" +
                         $"Таможня: {item.CustomsAmount:N0}\n" +
                         $"Логистика ({item.LogisticsPercent}%): {item.LogisticsAmount:N0}\n" +
                         $"Склад ({item.WarehousePercent}%): {item.WarehouseAmount:N0}\n" +
                         $"Декларация ({item.DeclarationPercent}%): {item.DeclarationAmount:N0}\n" +
                         $"Сертификация ({item.CertificationPercent}%): {item.CertificationAmount:N0}\n" +
                         $"МЧС ({item.MchsPercent}%): {item.MchsAmount:N0}\n" +
                         $"Погрузка: {item.ShippingAmount:N0}\n" +
                         $"Отклонения ({item.DeviationPercent}%): {item.DeviationAmount:N0}\n\n" +
                         $"СЕБЕС ЗАКУП: {item.UnitCost:N0} UZS\n" +
                         $"ИТОГО ЗА ВСЕ: {item.TotalCost:N0} UZS";
            
            await DisplayAlert("Детали расчета", details, "Закрыть");
        }
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        if (BindingContext is BatchCostCalculationViewModel vm)
        {
            await DisplayAlert("Настройки", "Функция в разработке", "ОК");
        }
    }

    private async void OnRecalculateClicked(object sender, EventArgs e)
    {
        if (BindingContext is BatchCostCalculationViewModel vm)
        {
            await vm.RecalculateAsync();
            await DisplayAlert("✅ Готово", "Расчет выполнен успешно!", "ОК");
        }
    }
}
