using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;
using System.Collections.ObjectModel;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ContractsListViewModel : ObservableObject
{
    private readonly IContractsService _contracts;
    private readonly AuthService _auth;

    public bool IsAdmin => string.Equals(_auth.Role, "Admin", StringComparison.OrdinalIgnoreCase);

    public ObservableCollection<ContractListItem> Items { get; } = new();
    public List<string> Statuses { get; } = new() { "All", "Signed", "Paid", "PartiallyClosed", "Cancelled", "Closed" };

    private bool _suppressAutoRefresh;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string selectedStatus = "All";

    partial void OnSelectedStatusChanged(string value)
    {
        if (_suppressAutoRefresh) return;
        _ = RefreshAsync();
    }

    public ContractsListViewModel(IContractsService contracts, AuthService auth)
    {
        _contracts = contracts;
        _auth = auth;
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
        UpdateLocal(item, "Paid");
    }

    [RelayCommand]
    public async Task MarkClosedAsync(ContractListItem? item)
    {
        if (item is null) return;
        await _contracts.UpdateStatusAsync(item.Id, "Closed");
        UpdateLocal(item, "Closed");
    }

    [RelayCommand]
    public async Task MarkPartiallyClosedAsync(ContractListItem? item)
    {
        if (item is null) return;
        await _contracts.UpdateStatusAsync(item.Id, "PartiallyClosed");
        UpdateLocal(item, "PartiallyClosed");
    }

    [RelayCommand]
    public async Task MarkCancelledAsync(ContractListItem? item)
    {
        if (item is null) return;
        await _contracts.UpdateStatusAsync(item.Id, "Cancelled");
        UpdateLocal(item, "Cancelled");
    }

    private void UpdateLocal(ContractListItem item, string newStatus)
    {
        try
        {
            var idx = Items.IndexOf(item);
            if (idx >= 0)
            {
                var updated = new ContractListItem
                {
                    Id = item.Id,
                    OrgName = item.OrgName,
                    Inn = item.Inn,
                    Phone = item.Phone,
                    Status = newStatus,
                    CreatedAt = item.CreatedAt,
                    Note = item.Note
                };
                Items[idx] = updated; // triggers CollectionChanged -> UI updates
            }

            // Ensure the item stays visible: switch filter to All without reloading
            if (!string.Equals(SelectedStatus, "All", StringComparison.OrdinalIgnoreCase))
            {
                _suppressAutoRefresh = true;
                SelectedStatus = "All";
                _suppressAutoRefresh = false;
            }
        }
        catch { }
    }
}
