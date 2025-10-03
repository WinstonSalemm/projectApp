using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;
using System.Collections.ObjectModel;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ContractsListViewModel : ObservableObject
{
    private readonly IContractsService _contracts;

    public ObservableCollection<ContractListItem> Items { get; } = new();
    public List<string> Statuses { get; } = new() { "All", "Signed", "Paid", "Closed" };

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string selectedStatus = "All";

    partial void OnSelectedStatusChanged(string value)
    {
        _ = RefreshAsync();
    }

    public ContractsListViewModel(IContractsService contracts)
    {
        _contracts = contracts;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            IsBusy = true;
            Items.Clear();
            var status = SelectedStatus == "All" ? null : SelectedStatus;
            var list = await _contracts.ListAsync(status);
            foreach (var c in list)
                Items.Add(c);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task MarkPaidAsync(ContractListItem? item)
    {
        if (item is null) return;
        await _contracts.UpdateStatusAsync(item.Id, "Paid");
        await RefreshAsync();
    }

    [RelayCommand]
    public async Task MarkClosedAsync(ContractListItem? item)
    {
        if (item is null) return;
        await _contracts.UpdateStatusAsync(item.Id, "Closed");
        await RefreshAsync();
    }
}
