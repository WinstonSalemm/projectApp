using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ProjectApp.Client.Maui.Models;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ProductSelectViewModel : ObservableObject
{
    private readonly ICatalogService _catalog;
    private readonly IStocksService _stocks;
    private readonly ILogger<ProductSelectViewModel> _logger;

    [ObservableProperty] private string? query;
    [ObservableProperty] private string? selectedCategory;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private bool hasError;

    public class ProductRow
    {
        public int Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Nd40Qty { get; set; }
        public decimal Im40Qty { get; set; }
        public decimal TotalQty { get; set; }
    }

    public ObservableCollection<ProductRow> Results { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();

    [ObservableProperty] private bool isBusy;

    // Explicit command for XAML binding (fixes design-time 'SearchAsyncCommand not found')
    public IAsyncRelayCommand SearchAsyncCommand { get; }

    public ProductSelectViewModel(ICatalogService catalog, IStocksService stocks, ILogger<ProductSelectViewModel> logger)
    {
        _catalog = catalog;
        _stocks = stocks;
        _logger = logger;
        SearchAsyncCommand = new AsyncRelayCommand(SearchAsync);
        _ = LoadCategoriesAsync();
        _ = SearchAsync();
    }

    [RelayCommand]
    public async Task LoadCategoriesAsync()
    {
        try
        {
            IsBusy = true;
            HasError = false;
            ErrorMessage = null;
            _logger.LogInformation("[ProductSelectViewModel] LoadCategoriesAsync started");
            Categories.Clear();
            var cats = await _catalog.GetCategoriesAsync();
            _logger.LogInformation("[ProductSelectViewModel] LoadCategoriesAsync received {Count} categories", cats?.Count() ?? 0);
            foreach (var c in cats) Categories.Add(c);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ProductSelectViewModel] LoadCategoriesAsync failed");
            HasError = true;
            ErrorMessage = $"Ошибка загрузки категорий: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        try
        {
            IsBusy = true;
            HasError = false;
            ErrorMessage = null;
            Results.Clear();
            
            _logger.LogInformation("[ProductSelectViewModel] SearchAsync started: query={Query}, category={Category}", Query, SelectedCategory);
            var list = await _catalog.SearchAsync(Query, SelectedCategory);
            _logger.LogInformation("[ProductSelectViewModel] SearchAsync received {Count} products", list?.Count() ?? 0);
            
            // Try to load stocks, but don't crash if it fails
            var stockList = Enumerable.Empty<dynamic>();
            try
            {
                stockList = (await _stocks.GetStocksAsync(Query, SelectedCategory)) ?? Enumerable.Empty<dynamic>();
                _logger.LogInformation("[ProductSelectViewModel] SearchAsync received {Count} stocks", stockList.Count());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ProductSelectViewModel] Failed to load stocks, continuing without stock info");
                // Temporary: show error to user
                await NavigationHelper.DisplayAlert("Ошибка загрузки остатков", $"Не удалось загрузить остатки: {ex.Message}", "OK");
            }
            
            var stockMap = stockList?.ToDictionary(s => (int)s.ProductId, s => s) ?? new Dictionary<int, dynamic>();
            foreach (var p in list ?? Enumerable.Empty<dynamic>())
            {
                dynamic? stock = null;
                stockMap.TryGetValue(p.Id, out stock);
                Results.Add(new ProductRow
                {
                    Id = p.Id,
                    Sku = p.Sku,
                    Name = p.Name,
                    Unit = p.Unit,
                    Price = p.Price,
                    Nd40Qty = stock != null ? (decimal)stock.Nd40Qty : 0,
                    Im40Qty = stock != null ? (decimal)stock.Im40Qty : 0,
                    TotalQty = stock != null ? (decimal)stock.TotalQty : 0
                });
            }
            _logger.LogInformation("[ProductSelectViewModel] SearchAsync completed: {Count} results", Results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ProductSelectViewModel] SearchAsync failed");
            HasError = true;
            ErrorMessage = $"Ошибка поиска товаров: {ex.Message}";
        }
        finally { IsBusy = false; }
    }
}

