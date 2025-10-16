using System.Collections.ObjectModel;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class AnalyticsViewModel : ObservableObject
{
    private readonly ILogger<AnalyticsViewModel> _logger;

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool hasNoStats;
    [ObservableProperty] private bool hasStats;
    [ObservableProperty] private string period = "month"; // "month" or "year"
    [ObservableProperty] private string periodLabel = "За текущий месяц";
    
    // Finance KPI
    [ObservableProperty] private decimal financeRevenue;
    [ObservableProperty] private decimal financeGrossProfit;
    [ObservableProperty] private decimal financeMargin;
    [ObservableProperty] private int financeSalesCount;
    [ObservableProperty] private double financeCostPercent;
    [ObservableProperty] private double financeProfitPercent;

    public class ManagerStatRow
    {
        public string ManagerName { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public decimal OwnClientsRevenue { get; set; }
        public double OwnClientsPercentage { get; set; }
        public int ClientsCount { get; set; }
        public double BarWidth { get; set; } // Ширина бара для графика (в пикселях)
    }
    
    public partial class ProductCostRow : ObservableObject
    {
        public int ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        
        [ObservableProperty] private decimal cost;
        
        public decimal AveragePrice { get; set; }
        public decimal MarginPercent => AveragePrice > 0 ? ((AveragePrice - Cost) / AveragePrice) * 100 : 0;
        public decimal ProfitPerUnit => AveragePrice - Cost;
    }

    public ObservableCollection<ManagerStatRow> ManagerStats { get; } = new();
    public ObservableCollection<ProductCostRow> ProductCosts { get; } = new();

    public AnalyticsViewModel(ILogger<AnalyticsViewModel> logger)
    {
        _logger = logger;
    }
    
    [RelayCommand]
    public async Task LoadFinanceKpi()
    {
        try
        {
            IsLoading = true;
            
            var client = new HttpClient 
            { 
                BaseAddress = new Uri("https://tranquil-upliftment-production.up.railway.app") 
            };

            // Определяем период
            var now = DateTime.UtcNow;
            DateTime from, to;
            
            if (Period == "year")
            {
                from = new DateTime(now.Year, 1, 1);
                to = new DateTime(now.Year + 1, 1, 1);
            }
            else
            {
                from = new DateTime(now.Year, now.Month, 1);
                to = from.AddMonths(1);
            }

            // Загружаем KPI (без авторизации для теста, потом добавим токен)
            var url = $"/api/finance/kpi?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
            var response = await client.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[AnalyticsViewModel] Failed to load finance KPI: {StatusCode}", response.StatusCode);
                return;
            }

            var kpi = await response.Content.ReadFromJsonAsync<FinanceKpiDto>();
            
            if (kpi != null)
            {
                await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
                {
                    FinanceRevenue = kpi.Revenue;
                    FinanceGrossProfit = kpi.GrossProfit;
                    FinanceMargin = kpi.Revenue > 0 ? (kpi.GrossProfit / kpi.Revenue) * 100 : 0;
                    FinanceSalesCount = kpi.SalesCount;
                    FinanceCostPercent = kpi.Revenue > 0 ? (double)(kpi.Cogs / kpi.Revenue) : 0;
                    FinanceProfitPercent = kpi.Revenue > 0 ? (double)(kpi.GrossProfit / kpi.Revenue) : 0;
                });
            }

            _logger.LogInformation("[AnalyticsViewModel] Loaded finance KPI");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AnalyticsViewModel] Failed to load finance KPI");
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    private class FinanceKpiDto
    {
        public decimal Revenue { get; set; }
        public decimal Cogs { get; set; }
        public decimal GrossProfit { get; set; }
        public int SalesCount { get; set; }
    }

    [RelayCommand]
    public async Task LoadManagerStats()
    {
        try
        {
            IsLoading = true;
            ManagerStats.Clear();

            var client = new HttpClient 
            { 
                BaseAddress = new Uri("https://tranquil-upliftment-production.up.railway.app") 
            };

            // Определяем период
            var now = DateTime.UtcNow;
            DateTime from, to;
            
            if (Period == "year")
            {
                from = new DateTime(now.Year, 1, 1); // С 1 января текущего года
                to = new DateTime(now.Year + 1, 1, 1); // До 1 января следующего года
            }
            else // month
            {
                from = new DateTime(now.Year, now.Month, 1); // С 1 числа текущего месяца
                to = from.AddMonths(1); // До 1 числа следующего месяца
            }

            // Загружаем статистику по менеджерам
            var url = $"/api/analytics/managers?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
            var response = await client.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[AnalyticsViewModel] Failed to load manager stats: {StatusCode}", response.StatusCode);
                return;
            }

            var stats = await response.Content.ReadFromJsonAsync<List<ManagerStatsDto>>();
            
            if (stats != null)
            {
                await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var maxRevenue = stats.Max(s => s.TotalRevenue);
                    const double maxBarWidth = 300.0; // Максимальная ширина бара в пикселях
                    
                    foreach (var stat in stats.OrderByDescending(s => s.TotalRevenue))
                    {
                        var barWidth = maxRevenue > 0 
                            ? (double)(stat.TotalRevenue / maxRevenue) * maxBarWidth 
                            : 0;
                        
                        ManagerStats.Add(new ManagerStatRow
                        {
                            ManagerName = stat.ManagerDisplayName ?? stat.ManagerUserName ?? "Неизвестно",
                            TotalRevenue = stat.TotalRevenue,
                            OwnClientsRevenue = stat.OwnClientsRevenue,
                            OwnClientsPercentage = stat.TotalRevenue > 0 
                                ? (double)(stat.OwnClientsRevenue / stat.TotalRevenue * 100) 
                                : 0,
                            ClientsCount = stat.ClientsCount,
                            BarWidth = Math.Max(barWidth, 10) // Минимум 10px чтобы было видно
                        });
                    }
                    
                    // Проверяем есть ли хоть одна продажа
                    HasNoStats = stats.All(s => s.TotalRevenue == 0);
                    HasStats = !HasNoStats;
                });
            }

            _logger.LogInformation("[AnalyticsViewModel] Loaded {Count} manager stats, HasNoStats={HasNoStats}", ManagerStats.Count, HasNoStats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AnalyticsViewModel] Failed to load manager stats");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private class ManagerStatsDto
    {
        public string? ManagerUserName { get; set; }
        public string? ManagerDisplayName { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal OwnClientsRevenue { get; set; }
        public int ClientsCount { get; set; }
    }
    
    [RelayCommand]
    public async Task LoadProductCosts()
    {
        try
        {
            IsLoading = true;
            ProductCosts.Clear();

            var client = new HttpClient 
            { 
                BaseAddress = new Uri("https://tranquil-upliftment-production.up.railway.app") 
            };

            // Загружаем товары с аналитикой
            var response = await client.GetAsync("/api/products?size=1000");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[AnalyticsViewModel] Failed to load products: {StatusCode}", response.StatusCode);
                return;
            }

            var pagedResult = await response.Content.ReadFromJsonAsync<PagedResultDto>();
            
            if (pagedResult?.Items != null)
            {
                await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
                {
                    foreach (var product in pagedResult.Items.OrderBy(p => p.Sku))
                    {
                        ProductCosts.Add(new ProductCostRow
                        {
                            ProductId = product.Id,
                            Sku = product.Sku,
                            Name = product.Name,
                            Cost = product.Cost,
                            AveragePrice = product.Price // Используем текущую цену как среднюю
                        });
                    }
                });
            }

            _logger.LogInformation("[AnalyticsViewModel] Loaded {Count} products", ProductCosts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AnalyticsViewModel] Failed to load products");
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    public async Task SaveProductCost(ProductCostRow product)
    {
        try
        {
            var client = new HttpClient 
            { 
                BaseAddress = new Uri("https://tranquil-upliftment-production.up.railway.app") 
            };

            var response = await client.PutAsJsonAsync($"/api/products/{product.ProductId}/cost", new { cost = product.Cost });
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[AnalyticsViewModel] Updated cost for product {ProductId}", product.ProductId);
                await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Microsoft.Maui.Controls.Application.Current!.MainPage!.DisplayAlert("Успех", "Себестоимость обновлена", "OK");
                });
            }
            else
            {
                _logger.LogWarning("[AnalyticsViewModel] Failed to update cost: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AnalyticsViewModel] Failed to save product cost");
        }
    }
    
    private class ProductDto
    {
        public int Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Cost { get; set; }
    }
    
    private class PagedResultDto
    {
        public List<ProductDto> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int Size { get; set; }
    }
}
