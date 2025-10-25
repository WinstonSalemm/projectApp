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

    [RelayCommand(AllowConcurrentExecutions = true)]
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

}
