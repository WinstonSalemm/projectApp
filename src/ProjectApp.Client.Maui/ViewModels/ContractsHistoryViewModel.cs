using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ContractsHistoryViewModel : ObservableObject
{
    private readonly ApiContractsService _contracts;

    public class ContractHistoryRow
    {
        public int Id { get; set; }
        public int DisplayNo { get; set; } // sequential number within current year
        public string OrgName { get; set; } = string.Empty;
        public string? Inn { get; set; }
        public string? Phone { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? Note { get; set; }
    }

    public ObservableCollection<ContractHistoryRow> Items { get; } = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusFilter = string.Empty; // Signed/Paid/PartiallyClosed/Cancelled/Closed

    public ContractsHistoryViewModel(ApiContractsService contracts)
    {
        _contracts = contracts;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading) return;
        try
        {
            IsLoading = true;
            var status = string.IsNullOrWhiteSpace(StatusFilter) ? null : StatusFilter;
            var list = await _contracts.ListAsync(status);
            var yearStart = new DateTime(DateTime.UtcNow.Year, 1, 1);
            var currentYearItems = list
                .Where(c => c.CreatedAt >= yearStart)
                .OrderBy(c => c.CreatedAt)
                .ThenBy(c => c.Id)
                .ToList();

            int n = 1;
            var rows = currentYearItems.Select(c => new ContractHistoryRow
            {
                Id = c.Id,
                DisplayNo = n++,
                OrgName = c.OrgName,
                Inn = c.Inn,
                Phone = c.Phone,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
                Note = c.Note
            }).OrderByDescending(r => r.CreatedAt).ToList();

            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                Items.Clear();
                foreach (var r in rows)
                    Items.Add(r);
            });
        }
        finally { IsLoading = false; }
    }
}
