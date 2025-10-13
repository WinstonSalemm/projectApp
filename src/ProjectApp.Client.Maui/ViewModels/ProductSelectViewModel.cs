using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Models;
using ProjectApp.Client.Maui.Services;
using System.Collections.ObjectModel;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ProductSelectViewModel : ObservableObject
{
    private readonly ICatalogService _catalog;
    private readonly IStocksService _stocks;

    [ObservableProperty] private string? query;
    [ObservableProperty] private string? selectedCategory;

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

    public ProductSelectViewModel(ICatalogService catalog, IStocksService stocks)
    {
        _catalog = catalog;
        _stocks = stocks;
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
            Categories.Clear();
            var cats = await _catalog.GetCategoriesAsync();
            foreach (var c in cats) Categories.Add(c);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        try
        {
            IsBusy = true;
            Results.Clear();
            var list = await _catalog.SearchAsync(Query, SelectedCategory);
            var stockList = await _stocks.GetStocksAsync(Query, SelectedCategory);
            var stockMap = stockList.ToDictionary(s => s.ProductId, s => s);
            foreach (var p in list)
            {
                stockMap.TryGetValue(p.Id, out var stock);
                Results.Add(new ProductRow
                {
                    Id = p.Id,
                    Sku = p.Sku,
                    Name = p.Name,
                    Unit = p.Unit,
                    Price = p.Price,
                    Nd40Qty = stock?.Nd40Qty ?? 0,
                    Im40Qty = stock?.Im40Qty ?? 0,
                    TotalQty = stock?.TotalQty ?? 0
                });
            }
        }
        finally { IsBusy = false; }
    }
}

