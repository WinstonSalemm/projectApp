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
public class BatchCostItemDto : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private int _id;
    public int Id
    {
        get => _id;
        set { if (_id != value) { _id = value; OnPropertyChanged(); } }
    }

    private int _rowNumber;
    public int RowNumber
    {
        get => _rowNumber;
        set { if (_rowNumber != value) { _rowNumber = value; OnPropertyChanged(); } }
    }

    private string _productName = string.Empty;
    public string ProductName
    {
        get => _productName;
        set { if (_productName != value) { _productName = value; OnPropertyChanged(); } }
    }

    private int _quantity;
    public int Quantity
    {
        get => _quantity;
        set { if (_quantity != value) { _quantity = value; OnPropertyChanged(); } }
    }

    private decimal _priceRub;
    public decimal PriceRub
    {
        get => _priceRub;
        set { if (_priceRub != value) { _priceRub = value; OnPropertyChanged(); } }
    }

    private decimal _exchangeRate;
    public decimal ExchangeRate
    {
        get => _exchangeRate;
        set { if (_exchangeRate != value) { _exchangeRate = value; OnPropertyChanged(); } }
    }

    private decimal _priceUzs;
    public decimal PriceUzs
    {
        get => _priceUzs;
        set { if (_priceUzs != value) { _priceUzs = value; OnPropertyChanged(); } }
    }

    private decimal _priceSom;
    public decimal PriceSom
    {
        get => _priceSom;
        set { if (_priceSom != value) { _priceSom = value; OnPropertyChanged(); } }
    }

    private decimal _vatUzs;
    public decimal VatUzs
    {
        get => _vatUzs;
        set { if (_vatUzs != value) { _vatUzs = value; OnPropertyChanged(); } }
    }

    private decimal _logisticsUzs;
    public decimal LogisticsUzs
    {
        get => _logisticsUzs;
        set { if (_logisticsUzs != value) { _logisticsUzs = value; OnPropertyChanged(); } }
    }

    private decimal _storageUzs;
    public decimal StorageUzs
    {
        get => _storageUzs;
        set { if (_storageUzs != value) { _storageUzs = value; OnPropertyChanged(); } }
    }

    private decimal _declarationUzs;
    public decimal DeclarationUzs
    {
        get => _declarationUzs;
        set { if (_declarationUzs != value) { _declarationUzs = value; OnPropertyChanged(); } }
    }

    private decimal _certificationUzs;
    public decimal CertificationUzs
    {
        get => _certificationUzs;
        set { if (_certificationUzs != value) { _certificationUzs = value; OnPropertyChanged(); } }
    }

    private decimal _mChsUzs;
    public decimal MChsUzs
    {
        get => _mChsUzs;
        set { if (_mChsUzs != value) { _mChsUzs = value; OnPropertyChanged(); } }
    }

    private decimal _unforeseenUzs;
    public decimal UnforeseenUzs
    {
        get => _unforeseenUzs;
        set { if (_unforeseenUzs != value) { _unforeseenUzs = value; OnPropertyChanged(); } }
    }

    private decimal _customsUzs;
    public decimal CustomsUzs
    {
        get => _customsUzs;
        set { if (_customsUzs != value) { _customsUzs = value; OnPropertyChanged(); } }
    }

    private decimal _loadingUzs;
    public decimal LoadingUzs
    {
        get => _loadingUzs;
        set { if (_loadingUzs != value) { _loadingUzs = value; OnPropertyChanged(); } }
    }

    private decimal _unitCostUzs;
    public decimal UnitCostUzs
    {
        get => _unitCostUzs;
        set { if (_unitCostUzs != value) { _unitCostUzs = value; OnPropertyChanged(); } }
    }

    private decimal _unitCost;
    public decimal UnitCost
    {
        get => _unitCost;
        set { if (_unitCost != value) { _unitCost = value; OnPropertyChanged(); } }
    }

    private decimal _totalCostUzs;
    public decimal TotalCostUzs
    {
        get => _totalCostUzs;
        set { if (_totalCostUzs != value) { _totalCostUzs = value; OnPropertyChanged(); } }
    }

    private decimal _totalCost;
    public decimal TotalCost
    {
        get => _totalCost;
        set { if (_totalCost != value) { _totalCost = value; OnPropertyChanged(); } }
    }

    // Legacy fields (kept for compatibility with API responses)
    public decimal VatPercent { get; set; }
    public decimal CustomsAmount { get; set; }
    public decimal ShippingAmount { get; set; }
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
}
