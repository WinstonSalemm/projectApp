using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using ProjectApp.Client.Maui.Models;
using ProjectApp.Client.Maui.Services;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class QuickSaleViewModel : ObservableObject
{
    private readonly ICatalogService _catalog;
    private readonly ISalesService _sales;
    private readonly AppSettings _settings;

    private CancellationTokenSource? _searchCts;
    private string _lastSearchQuery = string.Empty;
    private enum LastAction { None, Search, Submit }
    private LastAction _lastAction = LastAction.None;
    private SaleDraft? _lastDraft;

    public ObservableCollection<ProductModel> SearchResults { get; } = new();
    public ObservableCollection<CartItemModel> Cart { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();

    public IReadOnlyList<PaymentType> PaymentTypes { get; } = new[]
    {
        PaymentType.CashWithReceipt,
        PaymentType.CashNoReceipt,
        PaymentType.CardWithReceipt,
        PaymentType.ClickWithReceipt,
        PaymentType.ClickNoReceipt,
        PaymentType.Click, // legacy
        PaymentType.Site,
        PaymentType.Return,
        PaymentType.Reservation,
        PaymentType.Payme,
    };

    [ObservableProperty]
    private string query = string.Empty;

    [ObservableProperty]
    private string? selectedCategory;

    [ObservableProperty]
    private PaymentType selectedPaymentType = PaymentType.CashWithReceipt;

    [ObservableProperty]
    private bool isReservation;

    [ObservableProperty]
    private bool isOffline = true;

    [ObservableProperty]
    private decimal total;

    [ObservableProperty]
    private string clientName = string.Empty;

    public ObservableCollection<string> ReservationNotes { get; } = new();
    [ObservableProperty]
    private string newReservationNote = string.Empty;

    public QuickSaleViewModel(ICatalogService catalog, ISalesService sales, AppSettings settings)
    {
        _catalog = catalog;
        _sales = sales;
        _settings = settings;

        // Offline banner based on settings
        IsOffline = !settings.UseApi;

        Cart.CollectionChanged += (_, __) => RecalculateTotalWithSubscriptions();
        IsReservation = SelectedPaymentType == PaymentType.Reservation;
        _ = LoadCategoriesAsync();
    }

    partial void OnQueryChanged(string value)
    {
        DebouncedSearchAsync(value);
    }

    private void DebouncedSearchAsync(string searchText)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token); // debounce 300ms
                if (token.IsCancellationRequested) return;
                _lastAction = LastAction.Search;
                _lastSearchQuery = searchText;
                var cat = string.IsNullOrWhiteSpace(SelectedCategory) ? null : SelectedCategory;
                var results = await _catalog.SearchAsync(searchText, cat, token);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    SearchResults.Clear();
                    foreach (var p in results)
                        SearchResults.Add(p);
                    if (_settings.UseApi)
                    {
                        IsOffline = false;
                    }
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                // If API mode and call failed, show offline banner
                if (_settings.UseApi)
                {
                    IsOffline = true;
                    try { await Toast.Make("Нет связи с сервером", ToastDuration.Short).Show(token); } catch { }
                }
            }
        }, token);
    }

    partial void OnSelectedCategoryChanged(string? value)
    {
        DebouncedSearchAsync(Query);
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var list = await _catalog.GetCategoriesAsync();
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                Categories.Clear();
                Categories.Add(string.Empty); // пустая категория = все
                foreach (var c in list)
                    Categories.Add(c);
            });
        }
        catch { }
    }

    [RelayCommand]
    private void AddToCart(ProductModel product)
    {
        if (product is null) return;
        var existing = Cart.FirstOrDefault(c => c.ProductId == product.Id);
        if (existing is null)
        {
            Cart.Add(new CartItemModel
            {
                ProductId = product.Id,
                Name = product.Name,
                UnitPrice = product.Price,
                Qty = 1d
            });
        }
        else
        {
            existing.Qty += 1d;
        }
        RecalculateTotalWithSubscriptions();
    }

    private void RecalculateTotalWithSubscriptions()
    {
        // Ensure we react to Qty changes
        foreach (var it in Cart)
        {
            it.PropertyChanged -= CartItemOnPropertyChanged;
            it.PropertyChanged += CartItemOnPropertyChanged;
        }
        RecalculateTotal();
    }

    private void CartItemOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CartItemModel.Qty) || e.PropertyName == nameof(CartItemModel.UnitPrice))
        {
            RecalculateTotal();
        }
    }

    private void RecalculateTotal()
    {
        Total = Cart.Sum(i => (decimal)i.Qty * i.UnitPrice);
    }

    partial void OnSelectedPaymentTypeChanged(PaymentType value)
    {
        IsReservation = value == PaymentType.Reservation;
    }

    [RelayCommand]
    private void RemoveFromCart(CartItemModel? item)
    {
        if (item == null) return;
        Cart.Remove(item);
        RecalculateTotal();
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (Cart.Count == 0) return;
        var draft = new SaleDraft
        {
            ClientName = string.IsNullOrWhiteSpace(ClientName) ? "Quick Client" : ClientName,
            PaymentType = SelectedPaymentType,
            Items = Cart.Select(c => new SaleDraftItem { ProductId = c.ProductId, Qty = c.Qty, UnitPrice = c.UnitPrice }).ToList()
        };
        if (SelectedPaymentType == PaymentType.Reservation && ReservationNotes.Any())
        {
            draft.ReservationNotes = ReservationNotes.ToList();
        }
        _lastAction = LastAction.Submit;
        _lastDraft = draft;
        SalesResult result;
        try
        {
            result = await _sales.SubmitSaleAsync(draft);
        }
        catch
        {
            result = SalesResult.Fail("Сетевая ошибка");
        }
        if (result.Success)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Application.Current!.MainPage!.DisplayAlert("Успех", "Продажа проведена", "OK");
            });
            Cart.Clear();
            Total = 0m;
        }
        else
        {
            if (_settings.UseApi)
            {
                IsOffline = true;
                var msg = string.IsNullOrWhiteSpace(result.ErrorMessage) ? "Нет связи с сервером" : result.ErrorMessage;
                try { await Toast.Make(msg, ToastDuration.Long).Show(); } catch { }
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var msg = string.IsNullOrWhiteSpace(result.ErrorMessage) ? "Не удалось провести продажу" : result.ErrorMessage;
                    await Application.Current!.MainPage!.DisplayAlert("Ошибка", msg, "OK");
                });
            }
        }
    }

    [RelayCommand]
    private async Task RetryAsync()
    {
        switch (_lastAction)
        {
            case LastAction.Search:
                DebouncedSearchAsync(_lastSearchQuery);
                break;
            case LastAction.Submit:
                if (_lastDraft is not null)
                {
                    try
                    {
                        var result = await _sales.SubmitSaleAsync(_lastDraft);
                        if (result.Success)
                        {
                            await MainThread.InvokeOnMainThreadAsync(async () =>
                            {
                                await Application.Current!.MainPage!.DisplayAlert("Успех", "Продажа проведена", "OK");
                            });
                            Cart.Clear();
                            Total = 0m;
                            IsOffline = false;
                        }
                        else
                        {
                            var msg = string.IsNullOrWhiteSpace(result.ErrorMessage) ? "Не удалось провести продажу" : result.ErrorMessage;
                            try { await Toast.Make(msg, ToastDuration.Long).Show(); } catch { }
                        }
                    }
                    catch { }
                }
                break;
            default:
                break;
        }
    }
}
