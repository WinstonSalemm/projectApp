using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;
using ProjectApp.Client.Maui.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace ProjectApp.Client.Maui.Views;

public partial class StocksPage : ContentPage
{
    public StocksPage(StocksViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private static string N(string? s) => (s ?? "").Trim().Replace(" ", string.Empty).Replace(',', '.');

    private async void OnDefectivesClicked(object sender, EventArgs e)
    {
        try
        {
            var page = App.Services.GetRequiredService<DefectivesPage>();
            await Navigation.PushAsync(page);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", $"Не удалось открыть страницу: {ex.Message}", "OK");
        }
    }

    private async void OnRefillsClicked(object sender, EventArgs e)
    {
        try
        {
            var page = App.Services.GetRequiredService<RefillsPage>();
            await Navigation.PushAsync(page);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", $"Не удалось открыть страницу: {ex.Message}", "OK");
        }
    }

    private async void OnAddToStockClicked(object sender, EventArgs e)
    {
        try
        {
            // 1) Название товара
            var name = await DisplayPromptAsync("Добавить на склад", "Название товара:", "Далее", "Отмена", "Огнетушитель ОП-4");
            if (string.IsNullOrWhiteSpace(name)) return;

            // 2) Количество (шт)
            var qtyText = await DisplayPromptAsync("Добавить на склад", "Количество (шт):", "Далее", "Отмена", "10", keyboard: Keyboard.Numeric);
            if (string.IsNullOrWhiteSpace(qtyText)) return;
            if (!decimal.TryParse(N(qtyText), NumberStyles.Any, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
            {
                await DisplayAlert("Ошибка", "Введите корректное количество", "OK");
                return;
            }

            // 3) Закупочная цена за единицу (UZS/шт)
            var unitCostText = await DisplayPromptAsync("Добавить на склад", "Закупочная цена (UZS за 1 шт):", "OK", "Отмена", "100000", keyboard: Keyboard.Numeric);
            if (string.IsNullOrWhiteSpace(unitCostText)) return;
            if (!decimal.TryParse(N(unitCostText), NumberStyles.Any, CultureInfo.InvariantCulture, out var unitCost) || unitCost <= 0)
            {
                await DisplayAlert("Ошибка", "Введите корректную цену за единицу", "OK");
                return;
            }

            // 4) Создаём товар (минимально: Name как Sku, единица "шт")
            var sp = App.Services;
            var products = sp.GetRequiredService<IProductsService>();
            var stocksApi = sp.GetRequiredService<ApiStocksService>();

            var draft = new ProductCreateDraft
            {
                Name = name.Trim(),
                Sku = name.Trim(),
                Unit = "шт",
                Price = 0m,
                Category = string.Empty
            };

            var productId = await products.CreateProductAsync(draft);
            if (productId == null)
            {
                await DisplayAlert("Ошибка", "Не удалось создать товар", "OK");
                return;
            }

            // 5) Создаём партию в IM-40 через /api/batches
            var ok = await stocksApi.CreateBatchAsync(productId.Value, qty, unitCost,
                note: "Ручное добавление со страницы склада (IM-40)",
                toIm40: true);

            if (!ok)
            {
                await DisplayAlert("Ошибка", "Не удалось создать партию/остаток", "OK");
                return;
            }

            await DisplayAlert("Готово", $"Товар добавлен на склад IM-40. Себестоимость за 1 шт: {unitCost:N2}", "OK");

            if (BindingContext is StocksViewModel vm)
            {
                await vm.RefreshCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }

    private async void OnEditProductTapped(object? sender, EventArgs e)
    {
        try
        {
            if (sender is not Frame frame) return;
            if (frame.BindingContext is not ProjectApp.Client.Maui.Services.StockViewModel stock) return;
            var services = App.Services;
            var editPage = services.GetService<ProductEditPage>();
            if (editPage == null) return;

            if (editPage.BindingContext is ProductEditViewModel vm)
            {
                vm.LoadFromParameters(stock.ProductId, stock.Sku, stock.Name, stock.Category, "шт");
                await vm.LoadCategoriesAsync();
            }

            var tcs = new TaskCompletionSource<(int Id, string Sku, string Name, string Category)>();
            void Handler(object? s, (int Id, string Sku, string Name, string Category) p) => tcs.TrySetResult(p);
            editPage.ProductUpdated += Handler;

            await Navigation.PushAsync(editPage);
            var updated = await tcs.Task;
            editPage.ProductUpdated -= Handler;

            await Navigation.PopAsync();

            if (BindingContext is StocksViewModel stocksVm)
            {
                await stocksVm.RefreshCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }
}

