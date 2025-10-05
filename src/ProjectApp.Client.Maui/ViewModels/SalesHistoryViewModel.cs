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
    private DateTime? dateFrom = DateTime.UtcNow.Date.AddDays(-7);

    [ObservableProperty]
    private DateTime? dateTo = DateTime.UtcNow.Date.AddDays(1);

    [ObservableProperty]
    private bool showAll;

    public SalesHistoryViewModel(ApiSalesService sales, AuthService auth, IServiceProvider services)
    {
        _sales = sales;
        _auth = auth;
        _services = services;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading) return;
        try
        {
            IsLoading = true;
            var isAdmin = string.Equals(_auth.Role, "Admin", StringComparison.OrdinalIgnoreCase);
            var list = await _sales.GetSalesAsync(DateFrom, DateTo, createdBy: isAdmin ? null : _auth.UserName, all: ShowAll);
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
                        CreatedBy = s.CreatedBy
                    });
                }
            });
        }
        finally { IsLoading = false; }
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
        await Application.Current!.MainPage!.Navigation.PushAsync(page);
    }
}
