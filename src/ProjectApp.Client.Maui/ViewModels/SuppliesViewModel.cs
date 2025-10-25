using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class SuppliesViewModel : ObservableObject
{
    private readonly ISuppliesService _suppliesService;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _currentTab = ""; // ND40 или IM40, по умолчанию пусто

    public ObservableCollection<SupplyDto> Nd40Supplies { get; } = new();
    public ObservableCollection<SupplyDto> Im40Supplies { get; } = new();

    public ObservableCollection<SupplyDto> CurrentSupplies =>
        CurrentTab == "ND40" ? Nd40Supplies : Im40Supplies;

    public bool IsNd40Visible => CurrentTab == "ND40";
    public bool IsIm40Visible => CurrentTab == "IM40";

    public SuppliesViewModel(ISuppliesService suppliesService)
    {
        _suppliesService = suppliesService;
    }

    [RelayCommand]
    private void SelectTab(string tab)
    {
        System.Diagnostics.Debug.WriteLine($"SelectTab called: {tab}");
        CurrentTab = tab;
        System.Diagnostics.Debug.WriteLine($"CurrentTab set to: {CurrentTab}");
        System.Diagnostics.Debug.WriteLine($"IsNd40Visible: {IsNd40Visible}, IsIm40Visible: {IsIm40Visible}");
        OnPropertyChanged(nameof(CurrentSupplies));
        OnPropertyChanged(nameof(IsNd40Visible));
        OnPropertyChanged(nameof(IsIm40Visible));
    }

    [RelayCommand]
    public async Task LoadSupplies()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;

            // Загружаем ND-40
            try
            {
                var nd40 = await _suppliesService.GetSuppliesAsync("ND40");
                Nd40Supplies.Clear();
                foreach (var supply in nd40)
                    Nd40Supplies.Add(supply);
                System.Diagnostics.Debug.WriteLine($"Loaded {Nd40Supplies.Count} ND-40 supplies");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading ND40: {ex}");
            }

            // Загружаем IM-40
            try
            {
                var im40 = await _suppliesService.GetSuppliesAsync("IM40");
                Im40Supplies.Clear();
                foreach (var supply in im40)
                    Im40Supplies.Add(supply);
                System.Diagnostics.Debug.WriteLine($"Loaded {Im40Supplies.Count} IM-40 supplies");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading IM40: {ex}");
            }

            OnPropertyChanged(nameof(CurrentSupplies));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadSupplies error: {ex}");
            try
            {
                await Shell.Current.DisplayAlert("Ошибка", $"Не удалось загрузить поставки: {ex.Message}", "ОК");
            }
            catch
            {
                // Ignore if can't show alert
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateSupply()
    {
        try
        {
            if (Shell.Current == null)
            {
                System.Diagnostics.Debug.WriteLine("Shell.Current is null");
                return;
            }

            var code = await Shell.Current.DisplayPromptAsync(
                "Новая поставка",
                "Введите № ГТД:",
                "Создать",
                "Отмена",
                placeholder: "ГТД-123");

            if (string.IsNullOrWhiteSpace(code))
                return;

            IsBusy = true;
            
            System.Diagnostics.Debug.WriteLine($"Creating supply with code: {code}");
            var newSupply = await _suppliesService.CreateSupplyAsync(code);
            
            if (newSupply == null)
            {
                System.Diagnostics.Debug.WriteLine("CreateSupplyAsync returned null");
                await Shell.Current.DisplayAlert("Ошибка", "Не удалось создать поставку", "ОК");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"Supply created: {newSupply.Code}, Type: {newSupply.RegisterType}");
            
            // Добавляем в нужную коллекцию
            if (newSupply.RegisterType == "ND40")
            {
                Nd40Supplies.Insert(0, newSupply);
                System.Diagnostics.Debug.WriteLine($"Added to ND40, total: {Nd40Supplies.Count}");
            }
            else
            {
                Im40Supplies.Insert(0, newSupply);
                System.Diagnostics.Debug.WriteLine($"Added to IM40, total: {Im40Supplies.Count}");
            }
            
            await Shell.Current.DisplayAlert("Успех", $"Поставка {code} создана в {newSupply.RegisterType}", "ОК");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateSupply error: {ex}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            
            try
            {
                if (Shell.Current != null)
                    await Shell.Current.DisplayAlert("Ошибка", $"Не удалось создать поставку: {ex.Message}", "ОК");
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("Failed to show error alert");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task EditSupply(SupplyDto supply)
    {
        if (supply.RegisterType == "IM40")
        {
            await Shell.Current.DisplayAlert("Внимание", "Поставки в IM-40 нельзя редактировать", "ОК");
            return;
        }

        // TODO: Открыть страницу редактирования позиций
        await Shell.Current.DisplayAlert("Редактирование", $"Открыть редактор для {supply.Code}", "ОК");
    }

    [RelayCommand]
    private async Task DeleteSupply(SupplyDto supply)
    {
        var confirm = await Shell.Current.DisplayAlert(
            "Подтверждение",
            $"Удалить поставку {supply.Code}?",
            "Да",
            "Нет");

        if (!confirm) return;

        try
        {
            IsBusy = true;
            await _suppliesService.DeleteSupplyAsync(supply.Id);
            await LoadSupplies();
            await Shell.Current.DisplayAlert("Успех", "Поставка удалена", "ОК");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Ошибка", $"Не удалось удалить: {ex.Message}", "ОК");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TransferToIm40(SupplyDto supply)
    {
        if (supply.RegisterType != "ND40")
        {
            await Shell.Current.DisplayAlert("Ошибка", "Можно переводить только из ND-40", "ОК");
            return;
        }

        var confirm = await Shell.Current.DisplayAlert(
            "Подтверждение",
            $"Перевести поставку {supply.Code} в IM-40?\n\nВсе партии будут перенесены автоматически.",
            "Да",
            "Нет");

        if (!confirm) return;

        try
        {
            IsBusy = true;
            await _suppliesService.TransferToIm40Async(supply.Id);
            await LoadSupplies();
            await Shell.Current.DisplayAlert("Успех", "Поставка переведена в IM-40", "ОК");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Ошибка", $"Не удалось перевести: {ex.Message}", "ОК");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenCosting(SupplyDto supply)
    {
        // Переход на страницу расчета себестоимости
        await Shell.Current.GoToAsync($"costing?supplyId={supply.Id}");
    }
}
