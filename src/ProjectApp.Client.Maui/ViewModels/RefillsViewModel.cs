using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class RefillsViewModel : ObservableObject
{
    private readonly RefillsApiService _apiService;
    private readonly ICatalogService _catalogService;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isCreatingRefill;

    // Список перезарядок
    public ObservableCollection<RefillRow> Refills { get; } = new();

    // Статистика
    [ObservableProperty]
    private string totalCostFormatted = "0";

    [ObservableProperty]
    private int totalQuantity;

    // Форма создания
    [ObservableProperty]
    private string? selectedProductName;

    [ObservableProperty]
    private string productIdText = "";

    [ObservableProperty]
    private int quantity = 1;

    [ObservableProperty]
    private int selectedWarehouse;  // 0 = ND40, 1 = IM40

    [ObservableProperty]
    private string warehouseName = "ND-40";

    [ObservableProperty]
    private string costPerUnitText = "";

    [ObservableProperty]
    private string? notes;

    [ObservableProperty]
    private string totalCostText = "0";

    [ObservableProperty]
    private bool canSubmit;

    public RefillsViewModel(RefillsApiService apiService, ICatalogService catalogService)
    {
        _apiService = apiService;
        _catalogService = catalogService;
    }

    /// <summary>
    /// Загрузить список перезарядок
    /// </summary>
    [RelayCommand]
    private async Task LoadRefillsAsync()
    {
        IsLoading = true;

        try
        {
            var refills = await _apiService.GetRefillsAsync();
            
            Refills.Clear();
            foreach (var r in refills)
            {
                Refills.Add(new RefillRow
                {
                    Id = r.Id,
                    ProductName = r.ProductName,
                    Sku = r.Sku ?? "",
                    Quantity = r.Quantity,
                    Warehouse = r.Warehouse == 0 ? "ND-40" : "IM-40",
                    CostPerUnit = FormatMoney(r.CostPerUnit),
                    TotalCost = FormatMoney(r.TotalCost),
                    Notes = r.Notes ?? "",
                    Status = r.Status == RefillStatus.Active ? "Активна" : "Отменена",
                    StatusColor = r.Status == RefillStatus.Active ? "#4CAF50" : "#808080",
                    CreatedBy = r.CreatedBy,
                    CreatedAt = r.CreatedAt.ToString("dd.MM.yyyy HH:mm"),
                    CanCancel = r.Status == RefillStatus.Active
                });
            }

            // Загрузить статистику
            var stats = await _apiService.GetStatsAsync();
            if (stats != null)
            {
                TotalCostFormatted = FormatMoney(stats.TotalCost);
                TotalQuantity = stats.TotalQuantity;
            }
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await NavigationHelper.DisplayAlert("Ошибка", ex.Message, "OK");
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Загрузить товар по ID
    /// </summary>
    [RelayCommand]
    private async Task LoadProductAsync()
    {
        if (string.IsNullOrWhiteSpace(ProductIdText) || !int.TryParse(ProductIdText, out var productId))
        {
            SelectedProductName = null;
            UpdateCanSubmit();
            return;
        }

        try
        {
            var products = await _catalogService.SearchAsync(null, null);
            var product = products.FirstOrDefault(p => p.Id == productId);
            
            if (product != null)
            {
                SelectedProductName = $"{product.Name} ({product.Sku})";
            }
            else
            {
                SelectedProductName = "Товар не найден";
            }
            
            UpdateCanSubmit();
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await NavigationHelper.DisplayAlert("Ошибка", ex.Message, "OK");
            });
        }
    }

    /// <summary>
    /// Переключить склад
    /// </summary>
    [RelayCommand]
    private void ToggleWarehouse()
    {
        SelectedWarehouse = SelectedWarehouse == 0 ? 1 : 0;
        WarehouseName = SelectedWarehouse == 0 ? "ND-40" : "IM-40";
    }

    /// <summary>
    /// Создать перезарядку
    /// </summary>
    [RelayCommand]
    private async Task CreateRefillAsync()
    {
        if (!CanSubmit || IsCreatingRefill)
            return;

        if (!decimal.TryParse(CostPerUnitText, out var costPerUnit) || costPerUnit <= 0)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await NavigationHelper.DisplayAlert("Ошибка", "Введите корректную стоимость", "OK");
            });
            return;
        }

        var totalCost = Quantity * costPerUnit;

        var confirm = await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            return await NavigationHelper.DisplayAlert(
                "Подтверждение",
                $"Перезарядить {Quantity} шт. \"{SelectedProductName}\" на складе {WarehouseName}?\n\nОбщая стоимость: {FormatMoney(totalCost)}",
                "Да",
                "Нет");
        });

        if (!confirm)
            return;

        IsCreatingRefill = true;

        try
        {
            var productId = int.Parse(ProductIdText);
            
            var dto = new CreateRefillDto
            {
                ProductId = productId,
                Quantity = Quantity,
                Warehouse = SelectedWarehouse,
                CostPerUnit = costPerUnit,
                Notes = Notes
            };

            var (success, error) = await _apiService.CreateRefillAsync(dto);

            if (success)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await NavigationHelper.DisplayAlert("Успех", "Перезарядка создана", "OK");
                });

                // Очистить форму
                ProductIdText = "";
                SelectedProductName = null;
                Quantity = 1;
                CostPerUnitText = "";
                Notes = null;
                TotalCostText = "0";
                UpdateCanSubmit();

                // Обновить список
                await LoadRefillsAsync();
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await NavigationHelper.DisplayAlert("Ошибка", error ?? "Не удалось создать перезарядку", "OK");
                });
            }
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await NavigationHelper.DisplayAlert("Ошибка", ex.Message, "OK");
            });
        }
        finally
        {
            IsCreatingRefill = false;
        }
    }

    /// <summary>
    /// Отменить перезарядку
    /// </summary>
    [RelayCommand]
    private async Task CancelRefillAsync(RefillRow row)
    {
        var reason = await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            return await Application.Current!.Windows[0].Page!.DisplayPromptAsync(
                "Отмена перезарядки",
                "Укажите причину отмены:",
                "OK",
                "Отмена",
                placeholder: "Например: Дубликат записи");
        });

        if (string.IsNullOrWhiteSpace(reason))
            return;

        IsLoading = true;

        try
        {
            var (success, error) = await _apiService.CancelRefillAsync(row.Id, reason);

            if (success)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await NavigationHelper.DisplayAlert("Успех", "Перезарядка отменена", "OK");
                });

                await LoadRefillsAsync();
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await NavigationHelper.DisplayAlert("Ошибка", error ?? "Не удалось отменить", "OK");
                });
            }
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await NavigationHelper.DisplayAlert("Ошибка", ex.Message, "OK");
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnQuantityChanged(int value)
    {
        if (value < 1)
            Quantity = 1;
        UpdateCanSubmit();
        RecalculateTotal();
    }

    partial void OnCostPerUnitTextChanged(string value)
    {
        UpdateCanSubmit();
        RecalculateTotal();
    }

    private void RecalculateTotal()
    {
        if (decimal.TryParse(CostPerUnitText, out var cost))
        {
            var total = Quantity * cost;
            TotalCostText = FormatMoney(total);
        }
        else
        {
            TotalCostText = "0";
        }
    }

    partial void OnProductIdTextChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && int.TryParse(value, out _))
        {
            _ = LoadProductAsync();
        }
        UpdateCanSubmit();
    }

    private void UpdateCanSubmit()
    {
        CanSubmit = !string.IsNullOrWhiteSpace(ProductIdText) && 
                    int.TryParse(ProductIdText, out var id) && 
                    id > 0 && 
                    Quantity > 0 && 
                    !string.IsNullOrWhiteSpace(CostPerUnitText) &&
                    decimal.TryParse(CostPerUnitText, out var cost) && 
                    cost > 0 &&
                    SelectedProductName != null &&
                    SelectedProductName != "Товар не найден";
    }

    private string FormatMoney(decimal amount)
    {
        if (amount >= 1_000_000)
            return $"{amount / 1_000_000:F1} млн";
        if (amount >= 1_000)
            return $"{amount / 1_000:F0} тыс";
        return $"{amount:N0}";
    }
}

/// <summary>
/// Строка перезарядки для отображения
/// </summary>
public class RefillRow
{
    public int Id { get; set; }
    public string ProductName { get; set; } = "";
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
    public string Warehouse { get; set; } = "";
    public string CostPerUnit { get; set; } = "";
    public string TotalCost { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Status { get; set; } = "";
    public string StatusColor { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public bool CanCancel { get; set; }
}
