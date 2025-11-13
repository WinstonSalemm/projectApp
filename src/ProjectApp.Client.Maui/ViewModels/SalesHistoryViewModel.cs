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
    private string? errorMessage;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private DateTime? dateFrom = DateTime.UtcNow.Date.AddDays(-31);

    [ObservableProperty]
    private DateTime? dateTo = DateTime.UtcNow.Date.AddDays(1);

    // Выбранный период в виде строки для отображения в UI
    public string PeriodText => $"{FormatDate(DateFrom)} — {FormatDate(DateTo)}";

    private static string FormatDate(DateTime? dt)
    {
        if (dt is null) return "-";
        try
        {
            // Показываем локальную дату в формате dd.MM.yyyy
            return dt.Value.ToLocalTime().ToString("dd.MM.yyyy");
        }
        catch
        {
            return dt.Value.ToString("dd.MM.yyyy");
        }
    }

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

    // Обновляем текст периода при изменении дат
    partial void OnDateFromChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(PeriodText));
    }

    partial void OnDateToChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(PeriodText));
    }

    partial void OnShowAllChanged(bool value)
    {
        if (!IsLoading)
            _ = LoadAsync();
    }

    partial void OnNdImOnlyChanged(bool value)
    {
        if (!IsLoading)
            _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        System.Diagnostics.Debug.WriteLine("[SalesHistoryViewModel] LoadAsync START");
        if (IsLoading) return;
        try
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = null;
            System.Diagnostics.Debug.WriteLine("[SalesHistoryViewModel] Checking auth...");
            var isAdmin = string.Equals(_auth.Role, "Admin", StringComparison.OrdinalIgnoreCase);
            var createdBy = (ShowAll || isAdmin) ? null : _auth.UserName;

            // Интерпретируем выбранные даты как локальные дни: [from 00:00 local, to+1 00:00 local) и конвертируем в UTC
            DateTime? fromUtc = null;
            DateTime? toUtc = null;
            if (DateFrom.HasValue)
            {
                var fLocal = DateFrom.Value.Date;
                if (fLocal.Kind == DateTimeKind.Unspecified) fLocal = DateTime.SpecifyKind(fLocal, DateTimeKind.Local);
                fromUtc = fLocal.ToUniversalTime();
            }
            if (DateTo.HasValue)
            {
                var tLocal = DateTo.Value.Date.AddDays(1); // эксклюзивная верхняя граница — следующий день 00:00
                if (tLocal.Kind == DateTimeKind.Unspecified) tLocal = DateTime.SpecifyKind(tLocal, DateTimeKind.Local);
                toUtc = tLocal.ToUniversalTime();
            }

            System.Diagnostics.Debug.WriteLine($"[SalesHistoryViewModel] Calling API GetSalesAsync (ShowAll={ShowAll}, createdBy={createdBy ?? "null"}, fromUtc={fromUtc:o}, toUtc={toUtc:o}, nd40Transferred={(NdImOnly ? "true" : "null")})...");
            var list = await _sales.GetSalesAsync(fromUtc, toUtc, createdBy: createdBy, all: ShowAll, nd40Transferred: (NdImOnly ? true : (bool?)null));
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
            HasError = true;
            ErrorMessage = ex is HttpRequestException hre
                ? ("Ошибка загрузки истории: " + (hre.Message ?? "HTTP ошибка"))
                : ("Ошибка загрузки истории: " + ex.Message);
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


