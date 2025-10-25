using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public class BatchCostCalculationViewModel : INotifyPropertyChanged, IQueryAttributable
{
    private readonly IBatchCostService _batchCostService;
    private int _supplyId;
    private string _supplyCode = "";
    private decimal _totalCost;
    private bool _isLoading;

    public BatchCostCalculationViewModel(IBatchCostService batchCostService)
    {
        _batchCostService = batchCostService;
        Items = new ObservableCollection<BatchCostItemDto>();
    }

    public ObservableCollection<BatchCostItemDto> Items { get; }

    public string SupplyCode
    {
        get => _supplyCode;
        set => SetProperty(ref _supplyCode, value);
    }

    public decimal TotalCost
    {
        get => _totalCost;
        set => SetProperty(ref _totalCost, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey("supplyId"))
        {
            _supplyId = int.Parse(query["supplyId"].ToString()!);
        }
        
        if (query.ContainsKey("supplyCode"))
        {
            SupplyCode = query["supplyCode"].ToString()!;
        }
    }

    public async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            
            var items = await _batchCostService.GetItemsAsync(_supplyId);
            
            Items.Clear();
            int rowNumber = 1;
            foreach (var item in items)
            {
                item.RowNumber = rowNumber++;
                Items.Add(item);
            }
            
            var total = await _batchCostService.GetTotalCostAsync(_supplyId);
            TotalCost = total;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading data: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task AddItemAsync(string productName, int quantity, decimal priceRub)
    {
        try
        {
            IsLoading = true;
            
            await _batchCostService.AddItemAsync(_supplyId, productName, quantity, priceRub);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding item: {ex}");
            throw;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task DeleteItemAsync(int itemId)
    {
        try
        {
            IsLoading = true;
            
            await _batchCostService.DeleteItemAsync(itemId, _supplyId);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting item: {ex}");
            throw;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task RecalculateAsync()
    {
        try
        {
            IsLoading = true;
            
            await _batchCostService.RecalculateAsync(_supplyId);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error recalculating: {ex}");
            throw;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

// DTO для отображения товара в таблице
public class BatchCostItemDto
{
    public int Id { get; set; }
    public int RowNumber { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal PriceRub { get; set; }
    public decimal ExchangeRate { get; set; }
    public decimal PriceSom { get; set; }
    public decimal VatPercent { get; set; }
    
    // Доли от фиксированных сумм
    public decimal CustomsAmount { get; set; }
    public decimal ShippingAmount { get; set; }
    
    // Проценты и суммы
    public decimal LogisticsPercent { get; set; }
    public decimal LogisticsAmount { get; set; }
    public decimal WarehousePercent { get; set; }
    public decimal WarehouseAmount { get; set; }
    public decimal DeclarationPercent { get; set; }
    public decimal DeclarationAmount { get; set; }
    public decimal CertificationPercent { get; set; }
    public decimal CertificationAmount { get; set; }
    public decimal MchsPercent { get; set; }
    public decimal MchsAmount { get; set; }
    public decimal DeviationPercent { get; set; }
    public decimal DeviationAmount { get; set; }
    
    // Итоги
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}
