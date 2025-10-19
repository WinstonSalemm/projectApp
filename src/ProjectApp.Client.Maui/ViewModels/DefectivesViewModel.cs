using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class DefectivesViewModel : ObservableObject
{
    private readonly DefectivesApiService _apiService;
    private readonly ICatalogService _catalogService;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isCreatingDefective;

    // Список брака
    public ObservableCollection<DefectiveRow> Defectives { get; } = new();

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
    private string? reason;

    [ObservableProperty]
    private bool canSubmit;

    public DefectivesViewModel(DefectivesApiService apiService, ICatalogService catalogService)
    {
        _apiService = apiService;
        _catalogService = catalogService;
    }

    /// <summary>
    /// Загрузить список брака
    /// </summary>
    [RelayCommand]
    private async Task LoadDefectivesAsync()
    {
        IsLoading = true;

        try
        {
            var defectives = await _apiService.GetDefectivesAsync();
            
            Defectives.Clear();
            foreach (var d in defectives)
            {
                Defectives.Add(new DefectiveRow
                {
                    Id = d.Id,
                    ProductName = d.ProductName,
                    Sku = d.Sku ?? "",
                    Quantity = d.Quantity,
                    Warehouse = d.Warehouse == 0 ? "ND-40" : "IM-40",
                    Reason = d.Reason ?? "",
                    Status = d.Status == DefectiveStatus.Active ? "Списан" : "Отменен",
                    StatusColor = d.Status == DefectiveStatus.Active ? "#FF0000" : "#808080",
                    CreatedBy = d.CreatedBy,
                    CreatedAt = d.CreatedAt.ToString("dd.MM.yyyy HH:mm"),
                    CanCancel = d.Status == DefectiveStatus.Active
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
    /// Создать списание брака
    /// </summary>
    [RelayCommand]
    private async Task CreateDefectiveAsync()
    {
        if (!CanSubmit || IsCreatingDefective)
            return;

        var confirm = await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            return await NavigationHelper.DisplayAlert(
                "Подтверждение",
                $"Списать {Quantity} шт. товара \"{SelectedProductName}\" как брак со склада {WarehouseName}?",
                "Да",
                "Нет");
        });

        if (!confirm)
            return;

        IsCreatingDefective = true;

        try
        {
            var productId = int.Parse(ProductIdText);
            
            var dto = new CreateDefectiveDto
            {
                ProductId = productId,
                Quantity = Quantity,
                Warehouse = SelectedWarehouse,
                Reason = Reason
            };

            var (success, error) = await _apiService.CreateDefectiveAsync(dto);

            if (success)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await NavigationHelper.DisplayAlert("Успех", "Брак списан", "OK");
                });

                // Очистить форму
                ProductIdText = "";
                SelectedProductName = null;
                Quantity = 1;
                Reason = null;
                UpdateCanSubmit();

                // Обновить список
                await LoadDefectivesAsync();
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await NavigationHelper.DisplayAlert("Ошибка", error ?? "Не удалось списать брак", "OK");
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
            IsCreatingDefective = false;
        }
    }

    /// <summary>
    /// Отменить списание брака
    /// </summary>
    [RelayCommand]
    private async Task CancelDefectiveAsync(DefectiveRow row)
    {
        var reason = await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            return await Application.Current!.Windows[0].Page!.DisplayPromptAsync(
                "Отмена брака",
                "Укажите причину отмены:",
                "OK",
                "Отмена",
                placeholder: "Например: Ошибка, товар не бракованный");
        });

        if (string.IsNullOrWhiteSpace(reason))
            return;

        IsLoading = true;

        try
        {
            var (success, error) = await _apiService.CancelDefectiveAsync(row.Id, reason);

            if (success)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await NavigationHelper.DisplayAlert("Успех", "Брак отменен, товар возвращен на склад", "OK");
                });

                await LoadDefectivesAsync();
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
                    SelectedProductName != null &&
                    SelectedProductName != "Товар не найден";
    }
}

/// <summary>
/// Строка брака для отображения
/// </summary>
public class DefectiveRow
{
    public int Id { get; set; }
    public string ProductName { get; set; } = "";
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
    public string Warehouse { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "";
    public string StatusColor { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public bool CanCancel { get; set; }
}
