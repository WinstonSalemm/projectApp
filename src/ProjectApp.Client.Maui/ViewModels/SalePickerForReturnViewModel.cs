using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ProjectApp.Client.Maui.Services;
using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class SalePickerForReturnViewModel : ObservableObject
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<SalePickerForReturnViewModel> _logger;

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string searchQuery = string.Empty;
    [ObservableProperty] private string selectedSaleType = "Розница";
    
    public ObservableCollection<string> SaleTypes { get; } = new() { "Розница", "Договоры" };
    public ObservableCollection<SaleForReturnRow> Sales { get; } = new();
    private List<SaleForReturnRow> _allSales = new();

    public SalePickerForReturnViewModel(IHttpClientFactory httpFactory, ILogger<SalePickerForReturnViewModel> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    [RelayCommand]
    public async Task LoadSales()
    {
        try
        {
            IsLoading = true;
            var client = _httpFactory.CreateClient(HttpClientNames.Api);

            // Загружаем продажи за последние 30 дней
            var from = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-ddTHH:mm:ss");
            var to = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");
            var response = await client.GetAsync($"/api/sales?dateFrom={from}&dateTo={to}&all=true");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[SalePickerForReturnViewModel] Failed to load sales: {StatusCode}", response.StatusCode);
                return;
            }

            var salesData = await response.Content.ReadFromJsonAsync<List<SaleDto>>();
            if (salesData == null) return;

            // Загружаем информацию о существующих возвратах
            var returnsResponse = await client.GetAsync("/api/returns/history");
            var returnsData = new List<ReturnDto>();
            if (returnsResponse.IsSuccessStatusCode)
            {
                returnsData = await returnsResponse.Content.ReadFromJsonAsync<List<ReturnDto>>() ?? new();
            }

            var returnsBySaleId = returnsData.ToDictionary(r => r.RefSaleId, r => r);

            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                _allSales.Clear();
                foreach (var sale in salesData.OrderByDescending(s => s.Id))
                {
                    var hasReturn = returnsBySaleId.ContainsKey(sale.Id);
                    _allSales.Add(new SaleForReturnRow
                    {
                        SaleId = sale.Id,
                        Title = $"Продажа #{sale.Id}",
                        ClientName = sale.ClientName ?? "Неизвестный клиент",
                        Total = sale.Total,
                        PaymentType = sale.PaymentType,
                        PaymentTypeLabel = GetPaymentTypeLabel(sale.PaymentType),
                        ItemsCount = sale.Items?.Count ?? 0,
                        CreatedAt = sale.CreatedAt,
                        HasReturn = hasReturn,
                        ReturnStatusLabel = hasReturn ? "✓ Возврат оформлен" : "",
                        ReturnStatusColor = hasReturn ? Colors.Green : Colors.Transparent
                    });
                }

                ApplyFilter();
            });

            _logger.LogInformation("[SalePickerForReturnViewModel] Loaded {Count} sales", _allSales.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SalePickerForReturnViewModel] Failed to load sales");
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedSaleTypeChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filtered = _allSales.AsEnumerable();

        // Фильтр по типу продажи
        if (SelectedSaleType == "Розница")
        {
            // Розница: все кроме Contract
            filtered = filtered.Where(s => s.PaymentType != "Contract");
        }
        else if (SelectedSaleType == "Договоры")
        {
            // Только Contract
            filtered = filtered.Where(s => s.PaymentType == "Contract");
        }

        // Поиск
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var query = SearchQuery.ToLower();
            filtered = filtered.Where(s =>
                s.Title.ToLower().Contains(query) ||
                s.ClientName.ToLower().Contains(query) ||
                s.SaleId.ToString().Contains(query));
        }

        Sales.Clear();
        foreach (var sale in filtered)
        {
            Sales.Add(sale);
        }
    }

    [RelayCommand]
    private async Task SelectSale(SaleForReturnRow sale)
    {
        if (sale.HasReturn)
        {
            await Application.Current!.MainPage!.DisplayAlert("Ошибка", "Возврат для этой продажи уже оформлен", "OK");
            return;
        }

        // Навигация на страницу возврата с передачей SaleId
        var returnPage = App.Services.GetRequiredService<Views.ReturnForSalePage>();
        if (returnPage.BindingContext is ReturnForSaleViewModel vm)
        {
            await vm.LoadAsync(sale.SaleId);
        }

        await Shell.Current.Navigation.PushAsync(returnPage);
    }

    private static string GetPaymentTypeLabel(string paymentType)
    {
        return paymentType switch
        {
            "CashWithReceipt" => "💵 Наличные с чеком",
            "CashNoReceipt" => "💵 Наличные без чека",
            "CardWithReceipt" => "💳 Карта с чеком",
            "ClickWithReceipt" => "📱 Click с чеком",
            "ClickNoReceipt" => "📱 Click без чека",
            "Click" => "📱 Click",
            "Payme" => "📱 Payme",
            "Site" => "🌐 Сайт",
            "Contract" => "📄 Договор",
            "Reservation" => "🔖 Бронь",
            _ => paymentType
        };
    }

    private class SaleDto
    {
        public int Id { get; set; }
        public string? ClientName { get; set; }
        public decimal Total { get; set; }
        public string PaymentType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<SaleItemDto>? Items { get; set; }
    }

    private class SaleItemDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
    }

    private class ReturnDto
    {
        public int Id { get; set; }
        public int RefSaleId { get; set; }
    }
}

// Helper class for UI binding
public class SaleForReturnRow
{
    public int SaleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public string PaymentTypeLabel { get; set; } = string.Empty;
    public int ItemsCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool HasReturn { get; set; }
    public string ReturnStatusLabel { get; set; } = string.Empty;
    public Color ReturnStatusColor { get; set; } = Colors.Transparent;
}
