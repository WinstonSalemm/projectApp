using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

[QueryProperty(nameof(SupplyId), "supplyId")]
public partial class CostingViewModel : ObservableObject
{
    private readonly ICostingService _costingService;

    [ObservableProperty]
    private int _supplyId;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isFinalized;

    // Параметры расчета (все вводятся вручную)
    [ObservableProperty]
    private decimal _exchangeRate = 158.08m;

    [ObservableProperty]
    private decimal _vatPct = 0.22m;

    [ObservableProperty]
    private decimal _logisticsPct = 0.01m;

    [ObservableProperty]
    private decimal _storagePct = 0.005m;

    [ObservableProperty]
    private decimal _declarationPct = 0.01m;

    [ObservableProperty]
    private decimal _certificationPct = 0.01m;

    [ObservableProperty]
    private decimal _mChsPct = 0.005m;

    [ObservableProperty]
    private decimal _unforeseenPct = 0.015m;

    [ObservableProperty]
    private decimal _customsFeeAbs = 105000m;

    [ObservableProperty]
    private decimal _loadingAbs = 10000m;

    [ObservableProperty]
    private decimal _returnsAbs = 5000m;

    [ObservableProperty]
    private decimal _grandTotal;

    [ObservableProperty]
    private int? _currentSessionId;

    public ObservableCollection<CostingItemDto> Items { get; } = new();

    public CostingViewModel(ICostingService costingService)
    {
        _costingService = costingService;
    }

    partial void OnSupplyIdChanged(int value)
    {
        if (value > 0)
        {
            _ = LoadSession();
        }
    }

    private async Task LoadSession()
    {
        try
        {
            IsBusy = true;

            // Загружаем последнюю сессию для этой поставки (если есть)
            var sessions = await _costingService.GetSessionsAsync(SupplyId);
            var lastSession = sessions.OrderByDescending(s => s.CreatedAt).FirstOrDefault();

            if (lastSession != null)
            {
                CurrentSessionId = lastSession.Id;
                IsFinalized = lastSession.IsFinalized;

                // Загружаем детали сессии
                var details = await _costingService.GetSessionDetailsAsync(lastSession.Id);
                
                ExchangeRate = details.Session.ExchangeRate;
                VatPct = details.Session.VatPct;
                LogisticsPct = details.Session.LogisticsPct;
                StoragePct = details.Session.StoragePct;
                DeclarationPct = details.Session.DeclarationPct;
                CertificationPct = details.Session.CertificationPct;
                MChsPct = details.Session.MChsPct;
                UnforeseenPct = details.Session.UnforeseenPct;
                CustomsFeeAbs = details.Session.CustomsFeeAbs;
                LoadingAbs = details.Session.LoadingAbs;
                ReturnsAbs = details.Session.ReturnsAbs;
                GrandTotal = details.GrandTotal;

                Items.Clear();
                foreach (var item in details.Snapshots)
                    Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Ошибка", $"Не удалось загрузить сессию: {ex.Message}", "ОК");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateSession()
    {
        if (CurrentSessionId.HasValue)
        {
            await Shell.Current.DisplayAlert("Внимание", "Сессия уже создана. Используйте Пересчитать.", "ОК");
            return;
        }

        try
        {
            IsBusy = true;

            var request = new CreateCostingSessionRequest
            {
                SupplyId = SupplyId,
                ExchangeRate = ExchangeRate,
                VatPct = VatPct,
                LogisticsPct = LogisticsPct,
                StoragePct = StoragePct,
                DeclarationPct = DeclarationPct,
                CertificationPct = CertificationPct,
                MChsPct = MChsPct,
                UnforeseenPct = UnforeseenPct,
                CustomsFeeAbs = CustomsFeeAbs,
                LoadingAbs = LoadingAbs,
                ReturnsAbs = ReturnsAbs
            };

            var session = await _costingService.CreateSessionAsync(request);
            CurrentSessionId = session.Id;

            await Shell.Current.DisplayAlert("Успех", "Сессия расчета создана", "ОК");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Ошибка", $"Не удалось создать сессию: {ex.Message}", "ОК");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Recalculate()
    {
        if (!CurrentSessionId.HasValue)
        {
            await Shell.Current.DisplayAlert("Внимание", "Сначала создайте сессию расчета", "ОК");
            return;
        }

        if (IsFinalized)
        {
            await Shell.Current.DisplayAlert("Внимание", "Нельзя пересчитать зафиксированную сессию", "ОК");
            return;
        }

        try
        {
            IsBusy = true;

            var result = await _costingService.RecalculateAsync(CurrentSessionId.Value);

            if (result.Success)
            {
                // Перезагружаем данные
                await LoadSession();
                await Shell.Current.DisplayAlert("Успех", $"Расчет выполнен. Позиций: {result.SnapshotsCount}", "ОК");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Ошибка", $"Не удалось пересчитать: {ex.Message}", "ОК");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Finalize()
    {
        if (!CurrentSessionId.HasValue)
        {
            await Shell.Current.DisplayAlert("Внимание", "Нет активной сессии", "ОК");
            return;
        }

        if (IsFinalized)
        {
            await Shell.Current.DisplayAlert("Внимание", "Сессия уже зафиксирована", "ОК");
            return;
        }

        var confirm = await Shell.Current.DisplayAlert(
            "Подтверждение",
            "Зафиксировать расчет?\n\nПосле фиксации будут автоматически созданы партии товара с рассчитанной себестоимостью.\nИзменения станут невозможны.",
            "Да, зафиксировать",
            "Отмена");

        if (!confirm) return;

        try
        {
            IsBusy = true;

            var result = await _costingService.FinalizeAsync(CurrentSessionId.Value);

            if (result.Success)
            {
                IsFinalized = true;
                await Shell.Current.DisplayAlert(
                    "Успех", 
                    $"{result.Message}\n\nСоздано партий: {result.BatchesCreated}", 
                    "ОК");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Ошибка", $"Не удалось зафиксировать: {ex.Message}", "ОК");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
