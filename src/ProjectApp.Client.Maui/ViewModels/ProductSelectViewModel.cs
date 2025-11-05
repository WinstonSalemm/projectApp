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
    private readonly ISalesService _sales;
    private readonly SaleSession _session;
    private readonly ILogger<ProductSelectViewModel> _logger;

    [ObservableProperty] private string? query;
    [ObservableProperty] private string? selectedCategory = "Все категории";
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

    public partial class CartItem : ObservableObject
    {
        public int ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        [ObservableProperty] private decimal unitPrice;
        [ObservableProperty] private decimal qty = 1;
        
        public decimal Total => UnitPrice * Qty;
        
        partial void OnUnitPriceChanged(decimal value) => OnPropertyChanged(nameof(Total));
        partial void OnQtyChanged(decimal value) => OnPropertyChanged(nameof(Total));
    }

    public ObservableCollection<ProductRow> Results { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<CartItem> CartItems { get; } = new();
    
    [ObservableProperty] private decimal cartTotal;
    [ObservableProperty] private int? selectedClientId;
    [ObservableProperty] private string selectedClientName = "Выберите клиента...";

    [ObservableProperty] private bool isBusy;

    // Explicit command for XAML binding (fixes design-time 'SearchAsyncCommand not found')
    public IAsyncRelayCommand SearchAsyncCommand { get; }

    public ProductSelectViewModel(ICatalogService catalog, IStocksService stocks, ISalesService sales, SaleSession session, ILogger<ProductSelectViewModel> logger)
    {
        _catalog = catalog;
        _stocks = stocks;
        _sales = sales;
        _session = session;
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
            
            // Добавляем опцию "Все категории"
            Categories.Add("Все категории");
            
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
            if (IsBusy) return;
            IsBusy = true;
            HasError = false;
            ErrorMessage = null;
            Results.Clear();
            
            // Если выбрано "Все категории", передаем null для поиска по всем
            var categoryFilter = SelectedCategory == "Все категории" ? null : SelectedCategory;
            
            _logger.LogInformation("[ProductSelectViewModel] SearchAsync started: query={Query}, category={Category}", Query, categoryFilter);
            var list = await _catalog.SearchAsync(Query, categoryFilter);
            _logger.LogInformation("[ProductSelectViewModel] SearchAsync received {Count} products", list?.Count() ?? 0);
            
            // Try to load stocks, but don't crash if it fails
            var stockList = Enumerable.Empty<dynamic>();
            try
            {
                stockList = (await _stocks.GetStocksAsync(Query, categoryFilter)) ?? Enumerable.Empty<dynamic>();
                _logger.LogInformation("[ProductSelectViewModel] SearchAsync received {Count} stocks", stockList.Count());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ProductSelectViewModel] Failed to load stocks, continuing without stock info");
            }
            
            var stockMap = stockList?.ToDictionary(s => (int)s.ProductId, s => s) ?? new Dictionary<int, dynamic>();
            bool onlyIm40Visible = _session.PaymentType == PaymentType.CashWithReceipt
                                    || _session.PaymentType == PaymentType.CardWithReceipt
                                    || _session.PaymentType == PaymentType.ClickWithReceipt;
            bool onlyNd40Visible = _session.PaymentType == PaymentType.CashNoReceipt
                                   || _session.PaymentType == PaymentType.ClickNoReceipt
                                   || _session.PaymentType == PaymentType.Click // legacy
                                   || _session.PaymentType == PaymentType.Debt;
            foreach (var p in list ?? Enumerable.Empty<dynamic>())
            {
                dynamic? stock = null;
                stockMap.TryGetValue(p.Id, out stock);
                var nd = stock != null ? (decimal)stock.Nd40Qty : 0m;
                var im = stock != null ? (decimal)stock.Im40Qty : 0m;
                var total = stock != null ? (decimal)stock.TotalQty : (im + nd);
                var visible = onlyIm40Visible ? im : (onlyNd40Visible ? nd : total);
                Results.Add(new ProductRow
                {
                    Id = p.Id,
                    Sku = p.Sku,
                    Name = p.Name,
                    Unit = p.Unit,
                    Price = p.Price,
                    Nd40Qty = nd,
                    Im40Qty = im,
                    TotalQty = visible
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

    public void AddToCart(ProductRow product)
    {
        var existing = CartItems.FirstOrDefault(x => x.ProductId == product.Id);
        if (existing != null)
        {
            existing.Qty++;
        }
        else
        {
            var item = new CartItem
            {
                ProductId = product.Id,
                Sku = product.Sku,
                Name = product.Name,
                UnitPrice = product.Price,
                Qty = 1
            };
            item.PropertyChanged += (s, e) => RecalculateTotal();
            CartItems.Add(item);
        }
        RecalculateTotal();
    }

    public void RemoveFromCart(CartItem item)
    {
        CartItems.Remove(item);
        RecalculateTotal();
    }

    private void RecalculateTotal()
    {
        CartTotal = CartItems.Sum(x => x.Total);
    }

    public void SetClient(int? clientId, string clientName)
    {
        SelectedClientId = clientId;
        SelectedClientName = string.IsNullOrWhiteSpace(clientName) ? "Выберите клиента..." : clientName;
    }

    [RelayCommand]
    public async Task Checkout()
    {
        if (CartItems.Count == 0)
        {
            await NavigationHelper.DisplayAlert("Корзина пуста", "Добавьте товары для оформления продажи", "OK");
            return;
        }

        try
        {
            IsBusy = true;

            // Prepare sale draft
            var draft = new SaleDraft
            {
                ClientId = SelectedClientId,
                ClientName = SelectedClientId.HasValue ? SelectedClientName : string.Empty,
                PaymentType = _session.PaymentType,
                Items = CartItems.Select(item => new SaleDraftItem
                {
                    ProductId = item.ProductId,
                    Qty = (double)item.Qty,
                    UnitPrice = item.UnitPrice
                }).ToList()
            };

            // Submit sale
            var result = await _sales.SubmitSaleAsync(draft);

            if (result.Success)
            {
                await NavigationHelper.DisplayAlert("✅ Успех", $"Продажа оформлена!\nЧек #{result.SaleId}\nСумма: {CartTotal:N0} сум", "OK");
                
                // Clear cart and return to payment selection
                CartItems.Clear();
                RecalculateTotal();
                SelectedClientId = null;
                SelectedClientName = "Выберите клиента...";
                
                // Navigate back to payment select
                await NavigationHelper.PopAsync();
            }
            else
            {
                await NavigationHelper.DisplayAlert("❌ Ошибка", result.ErrorMessage ?? "Не удалось оформить продажу", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при оформлении продажи");
            await NavigationHelper.DisplayAlert("❌ Ошибка", $"Произошла ошибка: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }
}

