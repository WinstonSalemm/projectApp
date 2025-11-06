using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ReturnsHistoryViewModel : ObservableObject
{
    private readonly ApiReturnsService _returns;

    public ObservableCollection<ApiReturnsService.ReturnDto> Items { get; } = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private DateTime? dateFrom = DateTime.UtcNow.Date.AddDays(-7);

    [ObservableProperty]
    private DateTime? dateTo = DateTime.UtcNow.Date.AddDays(1);

    public ReturnsHistoryViewModel(ApiReturnsService returns)
    {
        _returns = returns;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading) return;
        try
        {
            IsLoading = true;
            var list = await _returns.QueryAsync(null, null, DateFrom, DateTo);
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                Items.Clear();
                foreach (var r in list)
                    Items.Add(r);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ReturnsHistoryViewModel] LoadAsync ERROR: {ex}");
        }
        finally { IsLoading = false; }
    }
}

