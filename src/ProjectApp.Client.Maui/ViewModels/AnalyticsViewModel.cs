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

    public class ManagerStatRow
    {
        public string ManagerName { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public decimal OwnClientsRevenue { get; set; }
        public double OwnClientsPercentage { get; set; }
        public int ClientsCount { get; set; }
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

            // Загружаем статистику по менеджерам
            var response = await client.GetAsync("/api/analytics/managers");
            
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
                    foreach (var stat in stats.OrderByDescending(s => s.OwnClientsRevenue))
                    {
                        ManagerStats.Add(new ManagerStatRow
                        {
                            ManagerName = stat.ManagerDisplayName ?? stat.ManagerUserName ?? "Неизвестно",
                            TotalRevenue = stat.TotalRevenue,
                            OwnClientsRevenue = stat.OwnClientsRevenue,
                            OwnClientsPercentage = stat.TotalRevenue > 0 
                                ? (double)(stat.OwnClientsRevenue / stat.TotalRevenue * 100) 
                                : 0,
                            ClientsCount = stat.ClientsCount
                        });
                    }
                });
            }

            _logger.LogInformation("[AnalyticsViewModel] Loaded {Count} manager stats", ManagerStats.Count);
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
