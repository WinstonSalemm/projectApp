using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;
using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class SalesHistoryViewModel : ObservableObject
{
    private readonly ApiSalesService _sales;
    private readonly AuthService _auth;
    private readonly IServiceProvider _services;

    public ObservableCollection<SaleModel> Items { get; } = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private DateTime? dateFrom = DateTime.UtcNow.Date.AddDays(-31);

    [ObservableProperty]
    private DateTime? dateTo = DateTime.UtcNow.Date.AddDays(1);

    [ObservableProperty]
    private bool showAll;

    [ObservableProperty]
    private bool ndImOnly; // Показывать только продажи с ND→IM позициями

    public SalesHistoryViewModel(ApiSalesService sales, AuthService auth, IServiceProvider services)
    {
        _sales = sales;
        _auth = auth;
        _services = services;
        // НЕ загружаем автоматически - загрузка будет вызвана вручную из SaleStartPage
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        System.Diagnostics.Debug.WriteLine("[SalesHistoryViewModel] LoadAsync START");
        if (IsLoading) return;
        try
        {
            IsLoading = true;
            System.Diagnostics.Debug.WriteLine("[SalesHistoryViewModel] Checking auth...");
            var isAdmin = string.Equals(_auth.Role, "Admin", StringComparison.OrdinalIgnoreCase);
            var createdBy = (ShowAll || isAdmin) ? null : _auth.UserName;
            System.Diagnostics.Debug.WriteLine($"[SalesHistoryViewModel] Calling API GetSalesAsync (ShowAll={ShowAll}, createdBy={createdBy ?? "null"}, nd40Transferred={(NdImOnly ? "true" : "null")})...");
            var list = await _sales.GetSalesAsync(DateFrom, DateTo, createdBy: createdBy, all: ShowAll, nd40Transferred: (NdImOnly ? true : (bool?)null));
            int count = list == null ? 0 : list.Count();
            System.Diagnostics.Debug.WriteLine($"[SalesHistoryViewModel] API returned {count} sales");
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                Items.Clear();
                foreach (var s in list)
                {
                    Enum.TryParse<PaymentType>(s.PaymentType, true, out var pt);
                    Items.Add(new SaleModel
                    {
                        Id = s.Id,
                        ClientId = s.ClientId,
                        ClientName = s.ClientName ?? string.Empty,
                        PaymentType = pt,
                        Total = s.Total,
                        CreatedAt = s.CreatedAt,
                        CreatedBy = s.CreatedBy,
                        Nd40Transferred = s.Nd40Transferred
                    });
                }
            });
            System.Diagnostics.Debug.WriteLine("[SalesHistoryViewModel] LoadAsync COMPLETED successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SalesHistoryViewModel] LoadAsync ERROR: {ex}");
            throw;
        }
        finally 
        { 
            IsLoading = false; 
            System.Diagnostics.Debug.WriteLine("[SalesHistoryViewModel] LoadAsync FINISHED");
        }
    }

    [RelayCommand]
    private async Task OpenReturnAsync(SaleModel? sale)
    {
        if (sale is null) return;
        var page = _services.GetService<ProjectApp.Client.Maui.Views.ReturnForSalePage>();
        if (page is null) return;
        if (page.BindingContext is ProjectApp.Client.Maui.ViewModels.ReturnForSaleViewModel vm)
        {
            await vm.LoadAsync(sale.Id);
        }
        await NavigationHelper.PushAsync(page);
    }

    [RelayCommand]
    private async Task OpenEditAsync(SaleModel? sale)
    {
        if (sale is null) return;
        var page = _services.GetService<ProjectApp.Client.Maui.Views.SaleEditPage>();
        if (page is null) return;
        if (page.BindingContext is ProjectApp.Client.Maui.ViewModels.SaleEditViewModel vm)
        {
            await vm.LoadAsync(sale.Id);
        }
        await NavigationHelper.PushAsync(page);
    }
}


