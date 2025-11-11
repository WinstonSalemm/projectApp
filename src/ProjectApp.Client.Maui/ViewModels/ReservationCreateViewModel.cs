using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;
using System.Collections.ObjectModel;
using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ReservationCreateViewModel : ObservableObject
{
    private readonly IReservationsService _reservations;

    [ObservableProperty] private int? clientId;
    [ObservableProperty] private string clientName = "Выберите клиента...";
    [ObservableProperty] private string? note;
    [ObservableProperty] private bool isBusy;

    public ObservableCollection<ItemRow> Items { get; } = new();

    public ReservationCreateViewModel(IReservationsService reservations)
    {
        _reservations = reservations;
    }

    public void SetClient(int? id, string name)
    {
        ClientId = id;
        ClientName = string.IsNullOrWhiteSpace(name) ? "Выберите клиента..." : name;
    }

    public void AddProduct(int productId, string sku, string name, decimal unitPrice)
    {
        Items.Add(new ItemRow
        {
            ProductId = productId,
            Sku = sku,
            Name = name,
            UnitPrice = unitPrice,
            Qty = 1,
            Register = StockRegister.IM40
        });
    }

    public void RemoveItem(ItemRow row)
    {
        Items.Remove(row);
    }

    [RelayCommand]
    public async Task SubmitAsync()
    {
        if (ClientId is null || ClientId <= 0)
        {
            await NavigationHelper.DisplayAlert("Клиент не выбран", "Выберите клиента для оформления брони", "OK");
            return;
        }
        if (Items.Count == 0)
        {
            await NavigationHelper.DisplayAlert("Пусто", "Добавьте хотя бы один товар", "OK");
            return;
        }
        try
        {
            IsBusy = true;
            var draft = new ReservationCreateDraft
            {
                ClientId = ClientId.Value,
                Paid = false,
                Note = Note,
                Items = Items.Select(i => new ReservationCreateItemDraft
                {
                    ProductId = i.ProductId,
                    Register = i.Register,
                    Qty = i.Qty
                }).ToList()
            };
            var id = await _reservations.CreateReservationAsync(draft, waitForPhoto: false, source: "Maui");
            if (id.HasValue)
            {
                await NavigationHelper.DisplayAlert("✅ Бронь создана", $"№{id.Value} оформлена на клиента {ClientName}", "OK");
                // navigate back to list
                await NavigationHelper.PopAsync();
            }
            else
            {
                await NavigationHelper.DisplayAlert("❌ Ошибка", "Не удалось создать бронь", "OK");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public partial class ItemRow : ObservableObject
    {
        public int ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        [ObservableProperty] private decimal qty;
        [ObservableProperty] private decimal unitPrice;
        [ObservableProperty] private StockRegister register;
        public decimal Total => Qty * UnitPrice;
    }
}
