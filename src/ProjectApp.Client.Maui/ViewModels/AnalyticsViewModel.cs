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

    public class ManagerStatRow
    {
        public string ManagerName { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public decimal OwnClientsRevenue { get; set; }
        public double OwnClientsPercentage { get; set; }
        public int ClientsCount { get; set; }
        public double BarWidth { get; set; } // Ширина бара для графика (в пикселях)
    }

    public ObservableCollection<ManagerStatRow> ManagerStats { get; } = new();

    public AnalyticsViewModel(ILogger<AnalyticsViewModel> logger)
    {
        _logger = logger;
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
}
