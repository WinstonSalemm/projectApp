using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class CashCollectionViewModel : ObservableObject
{
    private readonly CashCollectionApiService _apiService;
    private readonly AuthService _authService;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string? errorMessage;

    // Текущее накопление
    [ObservableProperty]
    private decimal currentAccumulated;

    [ObservableProperty]
    private string currentAccumulatedFormatted = "0";

    [ObservableProperty]
    private DateTime? lastCollectionDate;

    [ObservableProperty]
    private string lastCollectionDateFormatted = "Нет данных";

    // Неинкассированный остаток
    [ObservableProperty]
    private decimal totalRemaining;

    [ObservableProperty]
    private string totalRemainingFormatted = "0";

    // Форма инкассации
    [ObservableProperty]
    private string collectedAmountText = "";

    [ObservableProperty]
    private string notes = "";

    [ObservableProperty]
    private bool canSubmit;

    // История
    public ObservableCollection<CashCollectionHistoryRow> History { get; } = new();

    public CashCollectionViewModel(CashCollectionApiService apiService, AuthService authService)
    {
        _apiService = apiService;
        _authService = authService;
    }

    /// <summary>
    /// Загрузить данные
    /// </summary>
    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var summary = await _apiService.GetSummaryAsync();
            
            if (summary == null)
            {
                HasError = true;
                ErrorMessage = "Не удалось загрузить данные";
                return;
            }

            // Обновляем данные
            CurrentAccumulated = summary.CurrentAccumulated;
            CurrentAccumulatedFormatted = FormatMoney(summary.CurrentAccumulated);

            LastCollectionDate = summary.LastCollectionDate;
            LastCollectionDateFormatted = summary.LastCollectionDate.HasValue
                ? summary.LastCollectionDate.Value.ToString("dd.MM.yyyy HH:mm")
                : "Еще не было инкассаций";

            TotalRemaining = summary.TotalRemainingAmount;
            TotalRemainingFormatted = FormatMoney(summary.TotalRemainingAmount);

            // История
            History.Clear();
            foreach (var item in summary.History)
            {
                History.Add(new CashCollectionHistoryRow
                {
                    Id = item.Id,
                    Date = item.CollectionDate.ToString("dd.MM.yyyy HH:mm"),
                    Accumulated = FormatMoney(item.AccumulatedAmount),
                    Collected = FormatMoney(item.CollectedAmount),
                    Remaining = FormatMoney(item.RemainingAmount),
                    Notes = item.Notes ?? "",
                    CreatedBy = item.CreatedBy ?? ""
                });
            }

            HasError = false;
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Провести инкассацию
    /// </summary>
    [RelayCommand]
    private async Task SubmitCollectionAsync()
    {
        if (!CanSubmit)
            return;

        if (!decimal.TryParse(CollectedAmountText, out var amount) || amount <= 0)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await NavigationHelper.DisplayAlert("Ошибка", "Введите корректную сумму", "OK");
            });
            return;
        }

        if (amount > CurrentAccumulated)
        {
            var confirm = await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                return await NavigationHelper.DisplayConfirm(
                    "Внимание",
                    $"Вы хотите сдать {FormatMoney(amount)}, но накоплено только {CurrentAccumulatedFormatted}. Продолжить?",
                    "Да",
                    "Нет");
            });

            if (!confirm)
                return;
        }

        IsLoading = true;

        try
        {
            var (success, error) = await _apiService.CreateCollectionAsync(amount, Notes);

            if (success)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await NavigationHelper.DisplayAlert("Успех", "Инкассация проведена", "OK");
                });

                // Очистить форму
                CollectedAmountText = "";
                Notes = "";

                // Обновить данные
                await LoadDataAsync();
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await NavigationHelper.DisplayAlert("Ошибка", error ?? "Не удалось провести инкассацию", "OK");
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
    /// Удалить последнюю инкассацию
    /// </summary>
    [RelayCommand]
    private async Task DeleteLastAsync()
    {
        var confirm = await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            return await NavigationHelper.DisplayConfirm(
                "Подтверждение",
                "Удалить последнюю инкассацию? Это действие нельзя отменить!",
                "Удалить",
                "Отмена");
        });

        if (!confirm)
            return;

        IsLoading = true;

        try
        {
            var (success, error) = await _apiService.DeleteLastCollectionAsync();

            if (success)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await NavigationHelper.DisplayAlert("Успех", "Последняя инкассация удалена", "OK");
                });

                await LoadDataAsync();
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await NavigationHelper.DisplayAlert("Ошибка", error ?? "Не удалось удалить", "OK");
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

    partial void OnCollectedAmountTextChanged(string value)
    {
        CanSubmit = !string.IsNullOrWhiteSpace(value) && 
                    decimal.TryParse(value, out var amount) && 
                    amount > 0;
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
/// Строка истории инкассаций
/// </summary>
public class CashCollectionHistoryRow
{
    public int Id { get; set; }
    public string Date { get; set; } = "";
    public string Accumulated { get; set; } = "";
    public string Collected { get; set; } = "";
    public string Remaining { get; set; } = "";
    public string Notes { get; set; } = "";
    public string CreatedBy { get; set; } = "";
}
