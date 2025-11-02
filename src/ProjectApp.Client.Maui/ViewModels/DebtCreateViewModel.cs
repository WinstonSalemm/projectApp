using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Models.Dtos;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class DebtCreateViewModel : ObservableObject
{
    private readonly DebtorsApiService _api;

    [ObservableProperty]
    private int clientId;

    [ObservableProperty]
    private DateTime dueDate = DateTime.UtcNow.AddDays(14);

    [ObservableProperty]
    private string? notes;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    public ObservableCollection<DebtCreateItem> Items { get; } = new();

    public DebtCreateViewModel(DebtorsApiService api)
    {
        _api = api;
        // Стартовая строка для удобства
        Items.Add(new DebtCreateItem());
    }

    [RelayCommand]
    private void AddItem()
    {
        Items.Add(new DebtCreateItem());
    }

    [RelayCommand]
    private void RemoveItem(DebtCreateItem? item)
    {
        if (item != null && Items.Contains(item)) Items.Remove(item);
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ErrorMessage = null;

            if (ClientId <= 0)
            {
                ErrorMessage = "Укажите клиента (ID)";
                return;
            }

            var items = Items
                .Where(i => i.ProductId > 0 && i.Qty > 0 && i.Price > 0)
                .Select(i => new DebtCreateItemRequest
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName?.Trim() ?? string.Empty,
                    Sku = i.Sku,
                    Qty = i.Qty,
                    Price = i.Price
                })
                .ToList();

            if (items.Count == 0)
            {
                ErrorMessage = "Добавьте хотя бы одну позицию";
                return;
            }

            var req = new DebtCreateRequest
            {
                ClientId = ClientId,
                SaleId = 0, // опционально, если долг не привязан к продаже сейчас
                DueDate = DueDate,
                Notes = Notes,
                Items = items
            };

            var resp = await _api.CreateDebtAsync(req);
            if (resp?.Id > 0)
            {
                await NavigationHelper.DisplayAlert("Успех", $"Долг #{resp.Id} создан", "OK");
                // Очистка формы
                Items.Clear();
                Items.Add(new DebtCreateItem());
                Notes = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DebtCreateVM] Create error: {ex}");
            ErrorMessage = "Ошибка создания долга";
        }
        finally { IsBusy = false; }
    }
}

public partial class DebtCreateItem : ObservableObject
{
    [ObservableProperty]
    private int productId;

    [ObservableProperty]
    private string? productName;

    [ObservableProperty]
    private string? sku;

    [ObservableProperty]
    private decimal qty;

    [ObservableProperty]
    private decimal price;
}
