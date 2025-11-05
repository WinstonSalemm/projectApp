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

    public ObservableCollection<ReservationItemRow> Items { get; } = new();
    public ObservableCollection<ReservationPaymentRow> Payments { get; } = new();

    public ReservationDetailsViewModel(IReservationsService reservations)
    {
        _reservations = reservations;
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
}
