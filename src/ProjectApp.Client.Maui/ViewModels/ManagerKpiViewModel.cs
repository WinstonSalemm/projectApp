using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ManagerKpiViewModel : ObservableObject
{
    private readonly AnalyticsApiService _analyticsApi;

    [ObservableProperty]
    private ObservableCollection<ManagerKpiItemViewModel> managers = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    public ManagerKpiViewModel(AnalyticsApiService analyticsApi)
    {
        _analyticsApi = analyticsApi;
    }

    public async Task LoadManagersKpiAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var kpis = await _analyticsApi.GetTopManagersAsync(10);

            Managers.Clear();
            foreach (var kpi in kpis)
            {
                Managers.Add(new ManagerKpiItemViewModel
                {
                    UserName = kpi.UserName,
                    FullName = kpi.FullName,
                    SalesCount = kpi.SalesCount,
                    TotalRevenue = kpi.TotalRevenue,
                    AverageCheck = kpi.AverageCheck,
                    EfficiencyScore = kpi.EfficiencyScore,
                    Rank = kpi.Rank,
                    RankEmoji = GetRankEmoji(kpi.Rank)
                });
            }

            System.Diagnostics.Debug.WriteLine($"[ManagerKpiViewModel] Loaded {Managers.Count} managers");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ManagerKpiViewModel] LoadManagersKpiAsync error: {ex}");
            ErrorMessage = "Ошибка загрузки KPI менеджеров";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string GetRankEmoji(int rank)
    {
        return rank switch
        {
            1 => "🥇",
            2 => "🥈",
            3 => "🥉",
            _ => $"#{rank}"
        };
    }
}

public class ManagerKpiItemViewModel
{
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int SalesCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageCheck { get; set; }
    public decimal EfficiencyScore { get; set; }
    public int Rank { get; set; }
    public string RankEmoji { get; set; } = "";
}
