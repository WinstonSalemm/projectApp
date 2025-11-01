using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Models.Dtos;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class DebtDetailViewModel : ObservableObject
{
    private readonly DebtorsApiService _api;

    [ObservableProperty]
    private int debtId;

    [ObservableProperty]
    private DebtDetailsDto? debt;

    public ObservableCollection<DebtItemDto> Items { get; } = new();
    public ObservableCollection<DebtPaymentDto> Payments { get; } = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    public DebtDetailViewModel(DebtorsApiService api)
    {
        _api = api;
    }

    public async Task LoadAsync(int id)
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            DebtId = id;
            ErrorMessage = null;
            var details = await _api.GetDebtDetailsAsync(id);
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                Debt = details;
                Items.Clear();
                var list = details?.Items ?? new List<DebtItemDto>();
                foreach (var it in list) Items.Add(it);
            });
            var pays = await _api.GetDebtPaymentsAsync(id);
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                Payments.Clear();
                foreach (var p in pays) Payments.Add(p);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DebtDetailVM] Load error: {ex}");
            ErrorMessage = "Ошибка загрузки долга";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task PayAsync()
    {
        if (Debt is null) return;
        try
        {
            var amountStr = await PromptAsync("Введите сумму оплаты", "Оплата долга");
            if (string.IsNullOrWhiteSpace(amountStr)) return;
            if (!decimal.TryParse(amountStr, out var amount) || amount <= 0) return;

            var req = new PayDebtRequest { Amount = amount, PaymentMethod = "Cash" };
            await _api.PayDebtAsync(Debt.Id, req);
            await LoadAsync(Debt.Id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DebtDetailVM] Pay error: {ex}");
        }
    }

    private static async Task<string?> PromptAsync(string message, string title)
    {
        var page = ProjectApp.Client.Maui.Services.NavigationHelper.GetCurrentPage();
        if (page is null) return null;
        return await page.DisplayPromptAsync(title, message, "OK", "Отмена", keyboard: Microsoft.Maui.Keyboard.Numeric);
    }
}
