using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ContractsHistoryViewModel : ObservableObject
{
    private readonly ApiContractsService _contracts;

    public ObservableCollection<ContractListItem> Items { get; } = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusFilter = string.Empty; // Signed/Paid/Closed

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
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                Items.Clear();
                foreach (var c in list)
                    Items.Add(c);
            });
        }
        finally { IsLoading = false; }
    }
}
