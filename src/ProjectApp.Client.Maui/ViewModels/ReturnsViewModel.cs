using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using ProjectApp.Client.Maui;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ReturnsViewModel : ObservableObject
{
    private readonly IReturnsService _returns;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage = string.Empty;

    [ObservableProperty] private int refSaleId;
    [ObservableProperty] private int? clientId;
    [ObservableProperty] private string? reason;

    [ObservableProperty] private int newSaleItemId;
    [ObservableProperty] private decimal newQty = 1m;

    public ObservableCollection<ReturnDraftItem> Items { get; } = new();

    public ReturnsViewModel(IReturnsService returnsService)
    {
        _returns = returnsService;
    }

    [RelayCommand]
    private void AddItem()
    {
        if (NewSaleItemId <= 0 || NewQty <= 0) return;
        Items.Add(new ReturnDraftItem { SaleItemId = NewSaleItemId, Qty = NewQty });
        NewSaleItemId = 0; NewQty = 1m;
    }

    [RelayCommand]
    private void RemoveItem(ReturnDraftItem? item)
    {
        if (item is null) return;
        Items.Remove(item);
    }

    [RelayCommand]
    private async Task CreateReturnAsync()
    {
        if (RefSaleId <= 0) { StatusMessage = "Укажите номер продажи"; return; }
        try
        {
            IsBusy = true; StatusMessage = string.Empty;
            var draft = new ReturnDraft
            {
                RefSaleId = RefSaleId,
                ClientId = ClientId,
                Reason = Reason,
                Items = Items.Count == 0 ? null : Items.ToList()
            };
            var ok = await _returns.CreateReturnAsync(draft);
            StatusMessage = ok ? "Возврат создан" : "Ошибка возврата";
            if (ok)
            {
                Items.Clear(); Reason = null; ClientId = null; RefSaleId = 0;
                try
                {
                    var select = App.Services.GetRequiredService<ProjectApp.Client.Maui.Views.UserSelectPage>();
                    NavigationHelper.SetRoot(new NavigationPage(select));
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }
}


