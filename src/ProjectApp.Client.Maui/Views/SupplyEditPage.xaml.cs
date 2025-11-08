using ProjectApp.Client.Maui.ViewModels;
using ProjectApp.Client.Maui.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace ProjectApp.Client.Maui.Views;

public partial class SupplyEditPage : ContentPage
{
    private CostingPreviewViewModel? _costingVm;

    public SupplyEditPage(SupplyEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        // подтянем VM для предпросмотра
        var sp = App.Current?.Handler?.MauiContext?.Services;
        if (_costingVm == null && sp != null)
            _costingVm = sp.GetService<CostingPreviewViewModel>();

        if (_costingVm != null)
            CostingSection.BindingContext = _costingVm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            if (BindingContext is SupplyEditViewModel vm)
                await vm.LoadSupply();

            if (_costingVm != null && BindingContext is SupplyEditViewModel sVm && sVm.Supply?.Id > 0)
            {
                // гарантируем биндинг секции на VM расчёта
                CostingSection.BindingContext = _costingVm;
                _costingVm.SupplyId = sVm.Supply.Id;
                await _costingVm.RecalculateAsync();

                await Task.Delay(50);
                if (TableScroll is not null)
                    await TableScroll.ScrollToAsync(0, 0, false);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }

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

        var category = await DisplayPromptAsync("Добавить товар", "Категория:", "OK", "Отмена", "Пожарное оборудование");
        if (string.IsNullOrWhiteSpace(category)) return;

        var supplies = App.Current?.Handler?.MauiContext?.Services?.GetService<ISuppliesService>();
        if (supplies != null && vm.Supply.Id > 0)
        {
            await supplies.AddSupplyItemAsync(vm.Supply.Id, name, qty, priceRub, category, sku, weight);
            await vm.LoadSupply();
        }

        if (_costingVm != null)
        {
            await _costingVm.RecalculateAsync();
        }
    }

    private async void OnRemoveProductClicked(object sender, EventArgs e)
    {
        if (sender is not Button b || BindingContext is not SupplyEditViewModel vm) return;
        if (!await DisplayAlert("Удалить товар?", "Вы уверены?", "Да", "Нет")) return;

        await vm.RemoveProduct(b.CommandParameter);
        if (_costingVm != null)
        {
            await _costingVm.RecalculateAsync();
        }
    }

    private async void OnRecalculateClicked(object? sender, EventArgs e)
    {
        if (_costingVm == null) return;

        // общий курс (из Excel-поля) — только подставляем в существующие свойства, БЕЗ смены твоей логики
        if (!string.IsNullOrWhiteSpace(AnyFxEntry?.Text) &&
            decimal.TryParse(N(AnyFxEntry.Text), NumberStyles.Any, CultureInfo.InvariantCulture, out var fx))
        {
            _costingVm.RubToUzs = fx;
            _costingVm.UsdToUzs = fx;
        }

        await _costingVm.RecalculateAsync();
        await Task.Delay(30);
        await TableScroll.ScrollToAsync(0,0,false);
    }

    // ===== helpers =====
    private static string N(string? s) => (s ?? "").Trim().Replace(' ', '\u00A0').Replace(',', '.');
}
