using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;
using System.Collections.ObjectModel;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ReservationDetailsViewModel : ObservableObject
{
    private readonly IReservationsService _reservations;

    [ObservableProperty] private int id;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string? clientName;
    [ObservableProperty] private string? clientPhone;
    [ObservableProperty] private string createdBy = string.Empty;
    [ObservableProperty] private DateTime createdAt;
    [ObservableProperty] private DateTime reservedUntil;
    [ObservableProperty] private string status = string.Empty;
    [ObservableProperty] private bool paid;
    [ObservableProperty] private string? note;

    [ObservableProperty] private decimal total;
    [ObservableProperty] private decimal paidAmount;
    [ObservableProperty] private decimal dueAmount;

    [ObservableProperty] private decimal partialAmount;
    [ObservableProperty] private ReservationPaymentMethod selectedMethod = ReservationPaymentMethod.Cash;
    [ObservableProperty] private string? paymentNote;
    [ObservableProperty] private string? releaseReason;

    public ObservableCollection<ReservationItemRow> Items { get; } = new();
    public ObservableCollection<ReservationPaymentRow> Payments { get; } = new();

    public ReservationDetailsViewModel(IReservationsService reservations)
    {
        _reservations = reservations;
    }

    [RelayCommand]
    public async Task IncreaseQtyAsync(ReservationItemRow? row)
    {
        if (row is null) return;
        await UpdateQtyAsync(row.ProductId, row.Qty + 1);
    }

    [RelayCommand]
    public async Task DecreaseQtyAsync(ReservationItemRow? row)
    {
        if (row is null) return;
        var newQty = row.Qty - 1;
        if (newQty < 0) newQty = 0;
        await UpdateQtyAsync(row.ProductId, newQty);
    }

    [RelayCommand]
    public async Task RemoveItemAsync(ReservationItemRow? row)
    {
        if (row is null) return;
        await UpdateQtyAsync(row.ProductId, 0);
    }

    private async Task UpdateQtyAsync(int productId, decimal newQty)
    {
        if (Id <= 0) return;
        try
        {
            IsLoading = true;
            var map = Items
                .GroupBy(i => i.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));
            map[productId] = newQty;
            var list = map.Select(kv => new ReservationUpdateItem { ProductId = kv.Key, Qty = kv.Value }).ToList();
            var ok = await _reservations.UpdateItemsAsync(Id, list);
            if (ok)
            {
                await LoadAsync(Id);
            }
            else
            {
                await NavigationHelper.DisplayAlert("Ошибка", "Не удалось обновить количество", "OK");
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task AddProductAsync(int productId, decimal qty)
    {
        if (Id <= 0 || qty <= 0) return;
        try
        {
            IsLoading = true;
            // Build desired items = existing items + added product
            var map = Items
                .GroupBy(i => i.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));
            if (map.ContainsKey(productId)) map[productId] += qty; else map[productId] = qty;

            var list = map.Select(kv => new ReservationUpdateItem { ProductId = kv.Key, Qty = kv.Value }).ToList();
            var ok = await _reservations.UpdateItemsAsync(Id, list);
            if (ok)
            {
                await LoadAsync(Id);
            }
            else
            {
                await NavigationHelper.DisplayAlert("Ошибка", "Не удалось обновить бронь", "OK");
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadAsync(int reservationId)
    {
        if (IsLoading) return;
        try
        {
            IsLoading = true;
            Id = reservationId;
            var dto = await _reservations.GetReservationAsync(reservationId);
            if (dto == null) return;

            ClientName = dto.ClientName;
            ClientPhone = dto.ClientPhone;
            CreatedBy = dto.CreatedBy;
            CreatedAt = dto.CreatedAt;
            ReservedUntil = dto.ReservedUntil;
            Status = dto.Status;
            Paid = dto.Paid;
            Note = dto.Note;
            Total = dto.Total;
            PaidAmount = dto.PaidAmount;
            DueAmount = dto.DueAmount;

            Items.Clear();
            foreach (var it in dto.Items) Items.Add(it);
            Payments.Clear();
            foreach (var p in dto.Payments) Payments.Add(p);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task PayFullAsync()
    {
        if (Id <= 0 || DueAmount <= 0) return;
        var ok = await _reservations.PayAsync(Id, DueAmount, SelectedMethod, PaymentNote);
        if (ok)
        {
            await LoadAsync(Id);
        }
    }

    [RelayCommand]
    public async Task PayPartialAsync()
    {
        if (Id <= 0 || PartialAmount <= 0) return;
        var ok = await _reservations.PayAsync(Id, PartialAmount, SelectedMethod, PaymentNote);
        if (ok)
        {
            PartialAmount = 0;
            await LoadAsync(Id);
        }
    }

    [RelayCommand]
    public async Task FulfillAsync()
    {
        if (Id <= 0) return;
        var ok = await _reservations.FulfillAsync(Id);
        if (ok)
        {
            await LoadAsync(Id);
        }
    }

    [RelayCommand]
    public async Task ReleaseAsync()
    {
        if (Id <= 0) return;
        var ok = await _reservations.ReleaseAsync(Id, ReleaseReason);
        if (ok)
        {
            ReleaseReason = null;
            await LoadAsync(Id);
        }
    }
}
