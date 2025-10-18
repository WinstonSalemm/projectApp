using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class CommissionAgentsViewModel : ObservableObject
{
    private readonly AnalyticsApiService _analyticsApi;

    [ObservableProperty]
    private ObservableCollection<CommissionAgentItemViewModel> agents = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private decimal totalBalance;

    [ObservableProperty]
    private int totalAgents;

    [ObservableProperty]
    private string? errorMessage;

    public CommissionAgentsViewModel(AnalyticsApiService analyticsApi)
    {
        _analyticsApi = analyticsApi;
    }

    public async Task LoadAgentsAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var agentsDto = await _analyticsApi.GetCommissionAgentsAsync();

            Agents.Clear();
            foreach (var agent in agentsDto.OrderByDescending(a => a.CommissionBalance))
            {
                Agents.Add(new CommissionAgentItemViewModel
                {
                    ClientId = agent.ClientId,
                    ClientName = agent.ClientName,
                    Phone = agent.Phone ?? "",
                    CommissionBalance = agent.CommissionBalance,
                    CommissionAgentSince = agent.CommissionAgentSince
                });
            }

            TotalAgents = Agents.Count;
            TotalBalance = Agents.Sum(a => a.CommissionBalance);

            System.Diagnostics.Debug.WriteLine($"[CommissionAgentsViewModel] Loaded {TotalAgents} agents, balance: {TotalBalance:N0}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CommissionAgentsViewModel] LoadAgentsAsync error: {ex}");
            ErrorMessage = "Ошибка загрузки партнеров";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public class CommissionAgentItemViewModel
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public decimal CommissionBalance { get; set; }
    public DateTime? CommissionAgentSince { get; set; }
}
