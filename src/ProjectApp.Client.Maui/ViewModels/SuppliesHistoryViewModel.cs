using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class SuppliesHistoryViewModel : ObservableObject
{
    private readonly ApiSuppliesService _supplies;

    public ObservableCollection<ApiSuppliesService.SupplyBatchDto> Items { get; } = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string codeFilter = string.Empty;

    public SuppliesHistoryViewModel(ApiSuppliesService supplies)
    {
        _supplies = supplies;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading) return;
        try
        {
            IsLoading = true;
            var list = await _supplies.QueryAsync(string.IsNullOrWhiteSpace(CodeFilter) ? null : CodeFilter, null, null);
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                Items.Clear();
                foreach (var b in list)
                    Items.Add(b);
            });
        }
        finally { IsLoading = false; }
    }
}

