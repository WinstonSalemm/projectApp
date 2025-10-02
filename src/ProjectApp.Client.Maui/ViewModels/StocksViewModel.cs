using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class StocksViewModel : ObservableObject
{
    private readonly IStocksService _stocks;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private string query = string.Empty;
    [ObservableProperty] private string? selectedCategory;
    [ObservableProperty] private bool showBatches;
    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<StockViewModel> Items { get; } = new();
    public ObservableCollection<BatchStockViewModel> BatchItems { get; } = new();

    public StocksViewModel(IStocksService stocks, ICatalogService catalog)
    {
        _stocks = stocks;
        _ = LoadCategoriesAsync(catalog);
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsBusy = true; StatusMessage = string.Empty;
            Items.Clear();
            BatchItems.Clear();
            if (ShowBatches)
            {
                var blist = await _stocks.GetBatchesAsync(string.IsNullOrWhiteSpace(Query) ? null : Query, string.IsNullOrWhiteSpace(SelectedCategory) ? null : SelectedCategory);
                foreach (var it in blist) BatchItems.Add(it);
            }
            else
            {
                var list = await _stocks.GetStocksAsync(string.IsNullOrWhiteSpace(Query) ? null : Query, string.IsNullOrWhiteSpace(SelectedCategory) ? null : SelectedCategory);
                foreach (var it in list) Items.Add(it);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    private async Task LoadCategoriesAsync(ICatalogService catalog)
    {
        try
        {
            var cats = await catalog.GetCategoriesAsync();
            Categories.Clear();
            Categories.Add(string.Empty);
            foreach (var c in cats) Categories.Add(c);
        }
        catch { }
    }

    partial void OnShowBatchesChanged(bool value)
    {
        _ = RefreshAsync();
    }
}
