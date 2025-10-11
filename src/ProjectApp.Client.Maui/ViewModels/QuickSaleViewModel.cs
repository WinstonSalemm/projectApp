using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Media;
using ProjectApp.Client.Maui.Models;
using ProjectApp.Client.Maui.Services;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.Messaging;
using ProjectApp.Client.Maui.Messages;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class QuickSaleViewModel : ObservableObject
{
    private readonly ICatalogService _catalog;
    private readonly ISalesService _sales;
    private readonly IReservationsService _reservations;
    private readonly IStocksService _stocks;
    private readonly AppSettings _settings;
    private readonly AuthService _auth;

    private CancellationTokenSource? _searchCts;
    private string _lastSearchQuery = string.Empty;
    private enum LastAction { None, Search, Submit }
    private LastAction _lastAction = LastAction.None;
    private SaleDraft? _lastDraft;
    private string? _presetCategory; // category chosen on previous screen

    public class QuickProductRow
    {
        public int Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Nd40Qty { get; set; }
        public decimal Im40Qty { get; set; }
        public decimal TotalQty { get; set; }
    }

    // Public method for View to trigger search on appearing
    public void Refresh()
    {
        DebouncedSearchAsync(Query);
    }

    public ObservableCollection<QuickProductRow> SearchResults { get; } = new();
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
    private bool canSubmit;

    [ObservableProperty]
    private decimal total;

    [ObservableProperty]
    private string clientName = string.Empty;

    [ObservableProperty]
    private int? selectedClientId;

    // Reservation-specific: whether payment has already been taken
    [ObservableProperty]
    private bool reservationPaid;

    // Admin restriction
    [ObservableProperty]
    private bool isSaleAllowed = true;

    public ObservableCollection<string> ReservationNotes { get; } = new();
    [ObservableProperty]
    private string newReservationNote = string.Empty;

    [RelayCommand]
    private void AddReservationNote()
    {
        var note = (NewReservationNote ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(note)) return;
        ReservationNotes.Add(note);
        NewReservationNote = string.Empty;
    }

    [RelayCommand]
    private void RemoveReservationNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return;
        ReservationNotes.Remove(note);
    }

    public QuickSaleViewModel(ICatalogService catalog, ISalesService sales, IReservationsService reservations, IStocksService stocks, AppSettings settings, AuthService auth)
    {
        _catalog = catalog;
        _sales = sales;
        _reservations = reservations;
        _stocks = stocks;
        _settings = settings;
        _auth = auth;

        // Start without banner; show only if API mode fails
        IsOffline = false;

        Cart.CollectionChanged += (_, __) => RecalculateTotalWithSubscriptions();
        IsReservation = SelectedPaymentType == PaymentType.Reservation;
        _ = LoadCategoriesAsync();
        // Admin cannot conduct sales
        IsSaleAllowed = !string.Equals(_auth.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        UpdateCanSubmit();

        // Listen for client picker selection
        WeakReferenceMessenger.Default.Register<ClientPickedMessage>(this, (r, m) =>
        {
            SelectedClientId = m.ClientId;
            ClientName = m.Name;
        });
    }

    // Called from previous screen to fix the initial category before UI binds
    public void SetPresetCategory(string? category)
    {
        _presetCategory = category;
        SelectedCategory = category;
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
                // Require auth in API mode to avoid silent zero stocks for non-auth users
                if (_settings.UseApi && !_auth.IsAuthenticated)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        IsOffline = true;
                        try { await Toast.Make("Нужен вход: выполните вход как Менеджер и повторите поиск", ToastDuration.Short).Show(token); } catch { }
                    });
                    return;
                }
                _lastAction = LastAction.Search;
                _lastSearchQuery = searchText;
                var cat = string.IsNullOrWhiteSpace(SelectedCategory) ? _presetCategory : SelectedCategory;
                // 1) Catalog is mandatory
                var results = await _catalog.SearchAsync(searchText, cat, token);
                // 2) Stocks are optional, fallback to public availability if secured API fails
                IEnumerable<StockViewModel> stockList = Enumerable.Empty<StockViewModel>();
                Dictionary<int, (decimal Total, decimal Im40, decimal Nd40)>? availability = null;
                Dictionary<string, (decimal Total, decimal Im40, decimal Nd40)>? availabilityBySku = null;
                try
                {
                    stockList = await _stocks.GetStocksAsync(searchText, cat, token);
                }
                catch
                {
                    // ignore here; we'll try fallback below
                }
                bool needsFallback = (!stockList?.Any() ?? true) || (stockList.Any() && stockList.All(s => s.TotalQty == 0m));
                if (needsFallback && _settings.UseApi && _stocks is ProjectApp.Client.Maui.Services.ApiStocksService apiStocks1)
                {
                    try
                    {
                        var ids = results.Select(r => r.Id);
                        availability = await apiStocks1.GetAvailabilityByProductIdsAsync(ids, token);
                    }
                    catch { }
                    // If still nothing meaningful, try by SKUs
                    try
                    {
                        if (availability == null || availability.Count == 0)
                        {
                            var skus = results.Select(r => r.Sku);
                            availabilityBySku = await apiStocks1.GetAvailabilityBySkusAsync(skus, token);
                        }
                    }
                    catch { }
                }
                var stockMap = stockList.ToDictionary(s => s.ProductId, s => s);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    SearchResults.Clear();
                    foreach (var p in results)
                    {
                        stockMap.TryGetValue(p.Id, out var stock);
                        // default tuple values to satisfy definite assignment
                        (decimal Total, decimal Im40, decimal Nd40) availTuple = default;
                        var hasAvail = availability != null && availability.TryGetValue(p.Id, out availTuple);
                        if (!hasAvail && availabilityBySku != null)
                        {
                            var key = (p.Sku ?? string.Empty).ToUpperInvariant();
                            if (availabilityBySku.TryGetValue(key, out var a2))
                            {
                                hasAvail = true;
                                availTuple = (a2.Total, a2.Im40, a2.Nd40);
                            }
                        }
                        SearchResults.Add(new QuickProductRow
                        {
                            Id = p.Id,
                            Sku = p.Sku,
                            Name = p.Name,
                            Unit = p.Unit,
                            Price = p.Price,
                            Nd40Qty = hasAvail ? availTuple.Nd40 : (stock?.Nd40Qty ?? 0),
                            Im40Qty = hasAvail ? availTuple.Im40 : (stock?.Im40Qty ?? 0),
                            TotalQty = hasAvail ? availTuple.Total : (stock?.TotalQty ?? 0)
                        });
                    }
                    if (_settings.UseApi)
                    {
                        IsOffline = false;
                    }
                    // Clear preset after first successful application
                    _presetCategory = null;
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                // If API mode and call failed, show offline banner
                if (_settings.UseApi)
                {
                    IsOffline = true;
                    var msg = (ex.Message ?? string.Empty);
                    var authErr = msg.Contains("401") || msg.Contains("403");
                    var text = authErr
                        ? "Доступ запрещён или сессия истекла. Войдите как Менеджер и повторите."
                        : "Нет связи с сервером";
                    try { await Toast.Make(text, ToastDuration.Short).Show(token); } catch { }
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
                // Apply preset category even if Picker reset SelectedCategory during binding
                var keep = _presetCategory ?? SelectedCategory;
                if (!string.IsNullOrWhiteSpace(keep))
                {
                    if (!Categories.Contains(keep))
                        Categories.Add(keep);
                    SelectedCategory = null; // force change notification
                    SelectedCategory = keep;
                    DebouncedSearchAsync(Query);
                    _presetCategory = null;
                }
            });
        }
        catch { }
    }

    [RelayCommand]
    private void AddToCart(QuickProductRow product)
    {
        if (!IsSaleAllowed) return;
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
        UpdateCanSubmit();
    }

    private void CartItemOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CartItemModel.Qty) || e.PropertyName == nameof(CartItemModel.UnitPrice))
        {
            RecalculateTotal();
            UpdateCanSubmit();
        }
    }

    private void RecalculateTotal()
    {
        Total = Cart.Sum(i => (decimal)i.Qty * i.UnitPrice);
    }

    private void UpdateCanSubmit()
    {
        bool AllIntegersPositive() => Cart.All(i => i.Qty >= 1d && Math.Abs(i.Qty - Math.Round(i.Qty)) < 1e-9);
        var ok = IsSaleAllowed && Cart.Count > 0 && Cart.All(i => i.UnitPrice > 0m) && AllIntegersPositive();
        if (SelectedPaymentType == PaymentType.Reservation)
        {
            // For reservations: require client name to be filled
            ok = ok && !string.IsNullOrWhiteSpace(ClientName);
        }
        CanSubmit = ok;
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
        UpdateCanSubmit();
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (!IsSaleAllowed)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Application.Current!.MainPage!.DisplayAlert("Действие недоступно", "Роль Админ не проводит продажи.", "OK");
            });
            return;
        }
        if (Cart.Count == 0) return;
        // If Reservation type -> separate API flow
        if (SelectedPaymentType == PaymentType.Reservation)
        {
            // Build reservation draft with snapshot prices from cart
            var rd = new ReservationCreateDraft
            {
                ClientId = SelectedClientId,
                Paid = ReservationPaid,
                Note = ReservationNotes.Any() ? string.Join("; ", ReservationNotes) : null,
            };
            foreach (var c in Cart)
            {
                rd.Items.Add(new ReservationCreateItemDraft
                {
                    ProductId = c.ProductId,
                    // Prefer IM40 by default; register-specific UI может быть добавлен позже
                    Register = Models.StockRegister.IM40,
                    Qty = (decimal)c.Qty
                });
            }

            // Windows: create without photo (text notify immediately)
            if (DeviceInfo.Platform == DevicePlatform.WinUI || DeviceInfo.Platform == DevicePlatform.WinUI)
            {
                var resId = await _reservations.CreateReservationAsync(rd, waitForPhoto: false, source: "Windows");
                if (!resId.HasValue)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                        await Application.Current!.MainPage!.DisplayAlert("Ошибка", "Не удалось создать резерв", "OK"));
                    return;
                }
                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await Application.Current!.MainPage!.DisplayAlert("Успех", $"Резерв создан #{resId}", "OK"));
                Cart.Clear();
                Total = 0m;
                ReservationPaid = false;
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        var select = App.Services.GetRequiredService<ProjectApp.Client.Maui.Views.UserSelectPage>();
                        Application.Current!.MainPage = new NavigationPage(select);
                    }
                    catch { }
                });
                return;
            }

            // Android: create with WaitForPhoto and then capture + upload
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                var resId = await _reservations.CreateReservationAsync(rd, waitForPhoto: true, source: "Android");
                if (!resId.HasValue)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                        await Application.Current!.MainPage!.DisplayAlert("Ошибка", "Не удалось создать резерв", "OK"));
                    return;
                }

                try
                {
                    var photo = await MediaPicker.CapturePhotoAsync();
                    if (photo == null)
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                            await Application.Current!.MainPage!.DisplayAlert("Фото обязательно", "Для резерва требуется фото менеджера", "OK"));
                        return;
                    }
                    await using var stream = await photo.OpenReadAsync();
                    var okUp = await _reservations.UploadReservationPhotoAsync(resId.Value, stream, System.IO.Path.GetFileName(photo.FullPath) ?? "reserve.jpg");
                    if (!okUp)
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                            await Application.Current!.MainPage!.DisplayAlert("Ошибка", "Не удалось отправить фото в Telegram", "OK"));
                        return;
                    }
                }
                catch
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                        await Application.Current!.MainPage!.DisplayAlert("Ошибка", "Не удалось сделать фото. Повторите.", "OK"));
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await Application.Current!.MainPage!.DisplayAlert("Успех", $"Резерв создан #{resId}", "OK"));
                Cart.Clear();
                Total = 0m;
                ReservationPaid = false;
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        var select = App.Services.GetRequiredService<ProjectApp.Client.Maui.Views.UserSelectPage>();
                        Application.Current!.MainPage = new NavigationPage(select);
                    }
                    catch { }
                });
                return;
            }

            // Other platforms: fallback to no-photo create
            var resIdOther = await _reservations.CreateReservationAsync(rd, waitForPhoto: false, source: DeviceInfo.Platform.ToString());
            if (!resIdOther.HasValue)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await Application.Current!.MainPage!.DisplayAlert("Ошибка", "Не удалось создать резерв", "OK"));
                return;
            }
            await MainThread.InvokeOnMainThreadAsync(async () =>
                await Application.Current!.MainPage!.DisplayAlert("Успех", $"Резерв создан #{resIdOther}", "OK"));
            Cart.Clear();
            Total = 0m;
            ReservationPaid = false;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    var select = App.Services.GetRequiredService<ProjectApp.Client.Maui.Views.UserSelectPage>();
                    Application.Current!.MainPage = new NavigationPage(select);
                }
                catch { }
            });
            return;
        }

        var draft = new SaleDraft
        {
            ClientId = SelectedClientId,
            ClientName = string.IsNullOrWhiteSpace(ClientName) ? "Посетитель" : ClientName,
            PaymentType = SelectedPaymentType,
            Items = Cart.Select(c => new SaleDraftItem { ProductId = c.ProductId, Qty = c.Qty, UnitPrice = c.UnitPrice }).ToList()
        };
        // Android: hold text notification; we'll send photo+caption
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            draft.NotifyHold = true;
        }
        if (SelectedPaymentType == PaymentType.Reservation)
        {
            // Always include a generated description line with client and whether paid
            var notes = new List<string>();
            var client = string.IsNullOrWhiteSpace(ClientName) ? "(не указан)" : ClientName.Trim();
            var paidText = ReservationPaid ? "Да" : "Нет";
            notes.Add($"Клиент: {client}; Оплата: {paidText}");
            if (ReservationNotes.Any()) notes.AddRange(ReservationNotes);
            draft.ReservationNotes = notes;
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
            // If Android: mandatory front camera photo and upload before finalizing
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                try
                {
                    var photo = await MediaPicker.CapturePhotoAsync();
                    if (photo == null)
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await Application.Current!.MainPage!.DisplayAlert("Фото обязательно", "Для подтверждения требуется фото", "OK");
                        });
                        return;
                    }
                    if (!(result.SaleId.HasValue && result.SaleId.Value > 0))
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await Application.Current!.MainPage!.DisplayAlert("Ошибка", "Не удалось определить номер продажи для загрузки фото", "OK");
                        });
                        return;
                    }
                    await using var stream = await photo.OpenReadAsync();
                    var okUp = await _sales.UploadSalePhotoAsync(result.SaleId!.Value, stream, System.IO.Path.GetFileName(photo.FullPath) ?? "sale.jpg");
                    if (!okUp)
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await Application.Current!.MainPage!.DisplayAlert("Ошибка", "Не удалось отправить фото в Telegram", "OK");
                        });
                        return;
                    }
                }
                catch
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await Application.Current!.MainPage!.DisplayAlert("Ошибка", "Не удалось сделать фото. Повторите.", "OK");
                    });
                    return;
                }
            }
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Application.Current!.MainPage!.DisplayAlert("Успех", "Продажа проведена", "OK");
            });
            Cart.Clear();
            Total = 0m;
            ReservationPaid = false;
            // Navigate to account selection
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    var select = App.Services.GetRequiredService<ProjectApp.Client.Maui.Views.UserSelectPage>();
                    Application.Current!.MainPage = new NavigationPage(select);
                }
                catch { }
            });
        }
        else
        {
            // Decide if it's a network issue or a business/validation error
            var err = result.ErrorMessage ?? string.Empty;
            var isNetwork = string.IsNullOrWhiteSpace(err)
                            || err.Contains("Сетевая ошибка", StringComparison.OrdinalIgnoreCase)
                            || err.StartsWith("HTTP 5")
                            || err.StartsWith("HTTP 0");

            if (_settings.UseApi && isNetwork)
            {
                IsOffline = true;
                var msg = string.IsNullOrWhiteSpace(err) ? "Нет связи с сервером" : err;
                try { await Toast.Make(msg, ToastDuration.Long).Show(); } catch { }
            }
            else
            {
                // Show server-provided message (e.g., недостаточно остатков)
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var msg = string.IsNullOrWhiteSpace(err) ? "Не удалось провести продажу" : err;
                    await Application.Current!.MainPage!.DisplayAlert("Ошибка", msg, "OK");
                });
            }
        }
    }

    [RelayCommand]
    private async Task ChangePaymentTypeAsync()
    {
        try
        {
            // Go back to category page then to payment type selection
            var nav = Application.Current!.MainPage!.Navigation;
            await nav.PopAsync();
            await nav.PopAsync();
        }
        catch { }
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
                            await MainThread.InvokeOnMainThreadAsync(() =>
                            {
                                try
                                {
                                    var select = App.Services.GetRequiredService<ProjectApp.Client.Maui.Views.UserSelectPage>();
                                    Application.Current!.MainPage = new NavigationPage(select);
                                }
                                catch { }
                            });
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
