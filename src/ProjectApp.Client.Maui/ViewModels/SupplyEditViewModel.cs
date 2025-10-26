using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class SupplyEditViewModel : ObservableObject, IQueryAttributable
{
    private readonly ISuppliesService _suppliesService;
    private readonly IBatchCostService _batchCostService;
    
    [ObservableProperty]
    private SupplyDto _supply = new SupplyDto();
    
    [ObservableProperty]
    private bool _isBusy;
    
    [ObservableProperty]
    private decimal _totalCost;
    
    [ObservableProperty]
    private int _costItemsCount;
    
    // Параметры расчета НД-40
    [ObservableProperty]
    private decimal _exchangeRate = 158.08m;
    
    [ObservableProperty]
    private decimal _customsFee = 105000m;
    
    [ObservableProperty]
    private decimal _vatPercent = 22m;
    
    [ObservableProperty]
    private decimal _correctionPercent = 5m;
    
    [ObservableProperty]
    private decimal _securityPercent = 0.5m;
    
    public ObservableCollection<SupplyItemDto> SupplyItems { get; } = new();
    
    public bool IsNd40 => Supply?.RegisterType == "ND40";
    
    private int _supplyId;
    
    public SupplyEditViewModel(ISuppliesService suppliesService, IBatchCostService batchCostService)
    {
        _suppliesService = suppliesService;
        _batchCostService = batchCostService;
    }
    
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== ApplyQueryAttributes called with {query.Count} parameters");
            
            if (query.TryGetValue("supplyId", out var idObj))
            {
                System.Diagnostics.Debug.WriteLine($"supplyId found: {idObj}, type: {idObj?.GetType()}");
                
                if (idObj is string idStr && int.TryParse(idStr, out var id))
                {
                    _supplyId = id;
                    System.Diagnostics.Debug.WriteLine($"Parsed supplyId: {_supplyId}");
                }
                else if (idObj is int directId)
                {
                    _supplyId = directId;
                    System.Diagnostics.Debug.WriteLine($"Direct supplyId: {_supplyId}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to parse supplyId from: {idObj}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("supplyId not found in query parameters");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ApplyQueryAttributes error: {ex}");
        }
    }
    
    public async Task LoadSupply()
    {
        if (IsBusy) return;
        
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== LoadSupply started, supplyId: {_supplyId}");
            IsBusy = true;
            
            if (_supplyId <= 0)
            {
                System.Diagnostics.Debug.WriteLine("Invalid supplyId, creating default supply");
                Supply = new SupplyDto 
                { 
                    Id = 0,
                    Code = "Новая поставка",
                    RegisterType = "ND40",
                    Status = "Draft",
                    CreatedAt = DateTime.Now
                };
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Loading supply {_supplyId}...");
                
                // ✅ Загружаем реальную поставку с сервера
                var loadedSupply = await _suppliesService.GetSupplyByIdAsync(_supplyId);
                
                if (loadedSupply != null)
                {
                    Supply = loadedSupply;
                    System.Diagnostics.Debug.WriteLine($"Supply loaded from server: {Supply.Code}, Type: {Supply.RegisterType}");
                }
                else
                {
                    // Фоллбэк если не найдена
                    System.Diagnostics.Debug.WriteLine($"Supply {_supplyId} not found, creating fallback");
                    Supply = new SupplyDto 
                    { 
                        Id = _supplyId,
                        Code = $"Поставка #{_supplyId}",
                        RegisterType = "ND40",
                        Status = "Draft",
                        CreatedAt = DateTime.Now
                    };
                }
            }
            
            OnPropertyChanged(nameof(IsNd40));
            OnPropertyChanged(nameof(Supply));
            
            // Загружаем товары в поставке
            SupplyItems.Clear();
            if (_supplyId > 0)
            {
                var items = await _suppliesService.GetSupplyItemsAsync(_supplyId);
                foreach (var item in items)
                {
                    SupplyItems.Add(item);
                }
                System.Diagnostics.Debug.WriteLine($"Loaded {items.Count} items from server");
                
                // Загружаем расчёт себестоимости
                await LoadCostCalculation();
            }
            
            System.Diagnostics.Debug.WriteLine("LoadSupply completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadSupply error: {ex}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            
            try
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.DisplayAlert("Ошибка", $"Не удалось загрузить поставку: {ex.Message}", "ОК");
                }
            }
            catch
            {
                // Ignore
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    public async Task RemoveProduct(object item)
    {
        try
        {
            if (item == null)
            {
                System.Diagnostics.Debug.WriteLine("RemoveProduct: item is null");
                return;
            }
            
            if (item is SupplyItemDto supplyItem)
            {
                IsBusy = true;
                
                // Удаляем с сервера
                if (supplyItem.Id > 0)
                {
                    await _suppliesService.DeleteSupplyItemAsync(Supply.Id, supplyItem.Id);
                }
                
                // Удаляем из коллекции
                SupplyItems.Remove(supplyItem);
                
                if (Shell.Current != null)
                {
                    await Shell.Current.DisplayAlert("Удалено", "Товар удален из поставки", "ОК");
                }
                
                IsBusy = false;
            }
        }
        catch (Exception ex)
        {
            IsBusy = false;
            System.Diagnostics.Debug.WriteLine($"RemoveProduct error: {ex}");
            if (Shell.Current != null)
            {
                await Shell.Current.DisplayAlert("Ошибка", $"Не удалось удалить: {ex.Message}", "ОК");
            }
        }
    }
    
    private async Task LoadCostCalculation()
    {
        try
        {
            if (_supplyId <= 0) return;
            
            var items = await _batchCostService.GetItemsAsync(_supplyId);
            CostItemsCount = items.Count;
            
            var total = await _batchCostService.GetTotalCostAsync(_supplyId);
            TotalCost = total;
            
            System.Diagnostics.Debug.WriteLine($"Cost calculation loaded: {CostItemsCount} items, total: {TotalCost:N0}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadCostCalculation error: {ex}");
            CostItemsCount = 0;
            TotalCost = 0;
        }
    }
    
    public async Task RecalculateCost()
    {
        try
        {
            if (_supplyId <= 0 || !SupplyItems.Any())
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.DisplayAlert("Ошибка", "Добавьте товары в поставку", "ОК");
                }
                return;
            }
            
            IsBusy = true;
            
            await _batchCostService.RecalculateAsync(_supplyId);
            await LoadCostCalculation();
            
            if (Shell.Current != null)
            {
                await Shell.Current.DisplayAlert("✅ Готово", $"Расчет выполнен\n\nИтого: {TotalCost:N0} UZS", "ОК");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RecalculateCost error: {ex}");
            
            if (Shell.Current != null)
            {
                await Shell.Current.DisplayAlert("Ошибка", $"Ошибка расчета: {ex.Message}", "ОК");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
