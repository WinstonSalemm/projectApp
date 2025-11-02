using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ProjectApp.Client.Maui.Services;
using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ContractDetailsViewModel : ObservableObject
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ContractDetailsViewModel> _logger;

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private int contractId;
    [ObservableProperty] private string orgName = string.Empty;
    [ObservableProperty] private string inn = string.Empty;
    [ObservableProperty] private string phone = string.Empty;
    [ObservableProperty] private string status = string.Empty;
    [ObservableProperty] private string statusColor = "#6B7280";
    [ObservableProperty] private string contractType = string.Empty; // Open | Closed
    [ObservableProperty] private decimal totalAmount;
    [ObservableProperty] private decimal paidAmount;
    [ObservableProperty] private int totalItemsCount;
    [ObservableProperty] private int deliveredItemsCount;
    [ObservableProperty] private string note = string.Empty;
    [ObservableProperty] private bool ndWarning;

    public ObservableCollection<ContractItemRow> Items { get; } = new();
    public ObservableCollection<PaymentRow> Payments { get; } = new();
    public ObservableCollection<DeliveryRow> Deliveries { get; } = new();

    // UI свойства
    public decimal RemainingAmount => TotalAmount - PaidAmount;
    public decimal ItemsTotal => Items.Sum(i => i.UnitPrice * i.DeliveredQty); // израсходовано
    public decimal LimitRemaining => TotalAmount - ItemsTotal; // остаток лимита для Open
    public bool IsOpen => string.Equals(ContractType?.Trim(), "Open", StringComparison.OrdinalIgnoreCase);
    public bool IsPaid => PaidAmount >= TotalAmount;
    public bool IsFullyDelivered => DeliveredItemsCount >= TotalItemsCount;
    public bool CanClose => IsPaid && IsFullyDelivered && Status != "Closed";
    public string StatusLabel => GetStatusLabel(Status);

    public double SpentProgress => TotalAmount > 0 ? (double)(ItemsTotal / TotalAmount) : 0.0;
    public string RemainingTextColor
    {
        get
        {
            if (TotalAmount <= 0) return "#16A34A"; // green
            var ratio = LimitRemaining / TotalAmount; // доля остатка
            if (ratio <= 0.10m) return "#EF4444"; // red
            if (ratio <= 0.25m) return "#F59E0B"; // orange
            return "#16A34A"; // green
        }
    }

    public ContractDetailsViewModel(IHttpClientFactory httpFactory, ILogger<ContractDetailsViewModel> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    private HttpClient GetApiClient()
    {
        var client = _httpFactory.CreateClient(HttpClientNames.Api);
        if (client.BaseAddress == null)
        {
            try
            {
                var settings = App.Services.GetRequiredService<AppSettings>();
                var baseUrl = string.IsNullOrWhiteSpace(settings.ApiBaseUrl)
                    ? "https://tranquil-upliftment-production.up.railway.app"
                    : settings.ApiBaseUrl;
                client.BaseAddress = new Uri(baseUrl);
            }
            catch
            {
                client.BaseAddress = new Uri("https://tranquil-upliftment-production.up.railway.app");
            }
        }
        _logger.LogInformation("[ContractDetailsViewModel] Using BaseAddress: {Base}", client.BaseAddress);
        return client;
    }

    private string BuildUrl(string path)
    {
        var baseUrl = string.Empty;
        try
        {
            var settings = App.Services.GetRequiredService<AppSettings>();
            baseUrl = string.IsNullOrWhiteSpace(settings.ApiBaseUrl)
                ? "https://tranquil-upliftment-production.up.railway.app"
                : settings.ApiBaseUrl;
        }
        catch { }
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = "https://tranquil-upliftment-production.up.railway.app";
        return $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
    }

    [RelayCommand]
    public async Task LoadContract(int id)
    {
        try
        {
            IsLoading = true;
            ContractId = id;

            var client = GetApiClient();
            var response = await client.GetAsync(BuildUrl($"/api/contracts/{id}"));

            if (!response.IsSuccessStatusCode)
            {
                await Application.Current!.MainPage!.DisplayAlert("Ошибка", "Не удалось загрузить договор", "OK");
                return;
            }

            var contract = await response.Content.ReadFromJsonAsync<ContractDto>();
            if (contract == null) return;

            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                OrgName = contract.OrgName;
                Inn = contract.Inn ?? "";
                Phone = contract.Phone ?? "";
                Status = contract.Status;
                StatusColor = GetStatusColor(contract.Status);
                ContractType = contract.Type ?? string.Empty;
                TotalAmount = contract.TotalAmount;
                PaidAmount = contract.PaidAmount;
                TotalItemsCount = contract.TotalItemsCount;
                DeliveredItemsCount = contract.DeliveredItemsCount;
                Note = contract.Note ?? "";
                NdWarning = contract.NdWarning;

                Items.Clear();
                foreach (var item in contract.Items)
                {
                    Items.Add(new ContractItemRow
                    {
                        Id = item.Id,
                        Sku = item.Sku,
                        Name = item.Name,
                        Qty = item.Qty,
                        DeliveredQty = item.DeliveredQty,
                        UnitPrice = item.UnitPrice,
                        ProductId = item.ProductId
                    });
                }

                Payments.Clear();
                foreach (var payment in contract.Payments)
                {
                    Payments.Add(new PaymentRow
                    {
                        Amount = payment.Amount,
                        Method = payment.Method,
                        PaidAt = payment.PaidAt,
                        Note = payment.Note ?? ""
                    });
                }

                Deliveries.Clear();
                foreach (var delivery in contract.Deliveries)
                {
                    Deliveries.Add(new DeliveryRow
                    {
                        Id = delivery.Id,
                        ItemName = Items.FirstOrDefault(i => i.Id == delivery.ContractItemId)?.Name ?? "",
                        Qty = delivery.Qty,
                        DeliveredAt = delivery.DeliveredAt,
                        Note = delivery.Note ?? "",
                        Status = delivery.Status ?? "Completed",
                        UsedNd40 = delivery.UsedNd40
                    });
                }

                OnPropertyChanged(nameof(RemainingAmount));
                OnPropertyChanged(nameof(ItemsTotal));
                OnPropertyChanged(nameof(LimitRemaining));
                OnPropertyChanged(nameof(IsOpen));
                OnPropertyChanged(nameof(SpentProgress));
                OnPropertyChanged(nameof(RemainingTextColor));
                OnPropertyChanged(nameof(IsPaid));
                OnPropertyChanged(nameof(IsFullyDelivered));
                OnPropertyChanged(nameof(CanClose));
                OnPropertyChanged(nameof(StatusLabel));
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContractDetailsViewModel] Failed to load contract");
            await Application.Current!.MainPage!.DisplayAlert("Ошибка", ex.Message, "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddPayment()
    {
        var amountStr = await Application.Current!.MainPage!.DisplayPromptAsync(
            "Оплата", 
            $"Введите сумму (остаток: {RemainingAmount:N0})", 
            "OK", 
            "Отмена", 
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(amountStr)) return;

        if (!decimal.TryParse(amountStr, out var amount) || amount <= 0)
        {
            await Application.Current.MainPage.DisplayAlert("Ошибка", "Неверная сумма", "OK");
            return;
        }

        try
        {
            var client = GetApiClient();
            var dto = new { Amount = amount, Method = "BankTransfer", Note = (string?)null };
            var response = await client.PostAsJsonAsync(BuildUrl($"/api/contracts/{ContractId}/payments"), dto);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                await Application.Current.MainPage.DisplayAlert("Ошибка", error, "OK");
                return;
            }

            await Application.Current.MainPage.DisplayAlert("Успешно", "Оплата добавлена", "OK");
            await LoadContract(ContractId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContractDetailsViewModel] Failed to add payment");
            await Application.Current.MainPage.DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task DeliverItem(ContractItemRow item)
    {
        var remaining = item.Qty - item.DeliveredQty;
        var qtyStr = await Application.Current!.MainPage!.DisplayPromptAsync(
            "Отгрузка",
            $"{item.Name}\nДоступно: {remaining:N2}",
            "OK",
            "Отмена",
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(qtyStr)) return;

        if (!decimal.TryParse(qtyStr, out var qty) || qty <= 0 || qty > remaining)
        {
            await Application.Current.MainPage.DisplayAlert("Ошибка", "Неверное количество", "OK");
            return;
        }

        try
        {
            var client = GetApiClient();
            var dto = new { ContractItemId = item.Id, Qty = qty, Note = (string?)null };
            var response = await client.PostAsJsonAsync(BuildUrl($"/api/contracts/{ContractId}/deliveries"), dto);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                await Application.Current.MainPage.DisplayAlert("Ошибка", error, "OK");
                return;
            }

            // Определяем статус созданной отгрузки
            ContractDeliveryDto? created = null;
            try { created = await response.Content.ReadFromJsonAsync<ContractDeliveryDto>(); } catch { }
            if (created != null && string.Equals(created.Status, "PendingConversion", StringComparison.OrdinalIgnoreCase))
            {
                await Application.Current.MainPage.DisplayAlert("Ожидает конверсии", "Недостаточно на IM-40. Конвертируем из ND-40… Отгрузка помечена как ‘Ожидает конверсии’.", "OK");
            }
            else if (created != null && created.UsedNd40)
            {
                await Application.Current.MainPage.DisplayAlert("Отгрузка из ND-40", "Товар отгружен за счёт ND-40. После перевода поставки в IM-40 предупреждение исчезнет.", "OK");
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert("Успешно", "Товар отгружен", "OK");
            }
            await LoadContract(ContractId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContractDetailsViewModel] Failed to deliver item");
            await Application.Current.MainPage.DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task RetryConversion(DeliveryRow row)
    {
        try
        {
            var client = GetApiClient();
            var response = await client.PostAsync(BuildUrl($"/api/contracts/{ContractId}/shipments/{row.Id}/retry-conversion"), null);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                await Application.Current.MainPage.DisplayAlert("Ошибка", err, "OK");
                return;
            }
            await Application.Current.MainPage.DisplayAlert("Готово", "Попытка конверсии выполнена", "OK");
            await LoadContract(ContractId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContractDetailsViewModel] Retry conversion failed");
            await Application.Current.MainPage.DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task AddItem()
    {
        try
        {
            // Открываем страницу выбора товара в режиме picker
            var productPage = App.Services.GetRequiredService<Views.ProductSelectPage>();
            productPage.IsPicker = true;

            var tcs = new TaskCompletionSource<ProductSelectViewModel.ProductRow?>();
            void Handler(object? s, ProductSelectViewModel.ProductRow p) => tcs.TrySetResult(p);
            productPage.ProductPicked += Handler;
            await NavigationHelper.PushAsync(productPage);
            var picked = await tcs.Task;
            productPage.ProductPicked -= Handler;
            await NavigationHelper.PopAsync();
            if (picked == null) return;

            // Ввод количества и цены
            var qtyStr = await Application.Current!.MainPage!.DisplayPromptAsync(
                "Количество",
                picked.Name,
                "OK",
                "Отмена",
                keyboard: Keyboard.Numeric);
            if (string.IsNullOrWhiteSpace(qtyStr)) return;
            if (!decimal.TryParse(qtyStr, out var qty) || qty <= 0)
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка", "Неверное количество", "OK");
                return;
            }

            var priceStr = await Application.Current!.MainPage!.DisplayPromptAsync(
                "Цена",
                picked.Name,
                "OK",
                "Отмена",
                keyboard: Keyboard.Numeric);
            if (string.IsNullOrWhiteSpace(priceStr)) return;
            if (!decimal.TryParse(priceStr, out var price) || price < 0)
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка", "Неверная цена", "OK");
                return;
            }

            // Вызов API добавления позиции
            var client = GetApiClient();
            var dto = new
            {
                ProductId = (int?)picked.Id,
                Sku = picked.Sku,
                Name = picked.Name,
                Unit = "шт",
                Qty = qty,
                UnitPrice = price
            };
            var response = await client.PostAsJsonAsync(BuildUrl($"/api/contracts/{ContractId}/items"), dto);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                await Application.Current.MainPage.DisplayAlert("Ошибка", err, "OK");
                return;
            }

            // Для открытого договора: сразу создаем отгрузку на всю добавленную позицию
            if (IsOpen)
            {
                try
                {
                    var content = await response.Content.ReadAsStringAsync();
                    int newItemId = 0;
                    try
                    {
                        using var doc = JsonDocument.Parse(content);
                        if (doc.RootElement.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var i)) newItemId = i;
                        else if (doc.RootElement.TryGetProperty("Id", out var idEl2) && idEl2.TryGetInt32(out var j)) newItemId = j;
                    }
                    catch { }

                    if (newItemId > 0)
                    {
                        var deliveryDto = new { ContractItemId = newItemId, Qty = qty, Note = (string?)null };
                        var resp2 = await client.PostAsJsonAsync(BuildUrl($"/api/contracts/{ContractId}/deliveries"), deliveryDto);
                        // Игнорируем валидации, просто покажем ошибку если есть
                        if (!resp2.IsSuccessStatusCode)
                        {
                            var err2 = await resp2.Content.ReadAsStringAsync();
                            _logger.LogWarning("Auto-delivery failed: {Err}", err2);
                        }
                        else
                        {
                            try
                            {
                                var created2 = await resp2.Content.ReadFromJsonAsync<ContractDeliveryDto>();
                                if (created2 != null && created2.UsedNd40)
                                {
                                    await Application.Current.MainPage.DisplayAlert("Отгрузка из ND-40", "Товар отгружен за счёт ND-40. После перевода поставки в IM-40 предупреждение исчезнет.", "OK");
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Auto-delivery after add failed");
                }
            }

            await LoadContract(ContractId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContractDetailsViewModel] Failed to add item");
            await Application.Current.MainPage.DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task DeleteItem(ContractItemRow item)
    {
        var confirm = await Application.Current!.MainPage!.DisplayAlert(
            "Удалить позицию",
            $"Удалить {item.Name}?",
            "Да",
            "Отмена");

        if (!confirm) return;

        try
        {
            var client = GetApiClient();
            var response = await client.DeleteAsync(BuildUrl($"/api/contracts/{ContractId}/items/{item.Id}"));

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                await Application.Current.MainPage.DisplayAlert("Ошибка", error, "OK");
                return;
            }

            await Application.Current.MainPage.DisplayAlert("Успешно", "Позиция удалена", "OK");
            await LoadContract(ContractId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContractDetailsViewModel] Failed to delete item");
            await Application.Current.MainPage.DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task CloseContract()
    {
        var confirm = await Application.Current!.MainPage!.DisplayAlert(
            "Закрыть договор",
            "Все партии должны быть в IM-40. Продолжить?",
            "Да",
            "Отмена");

        if (!confirm) return;

        try
        {
            var client = GetApiClient();
            var response = await client.PostAsync(BuildUrl($"/api/contracts/{ContractId}/close"), null);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                await Application.Current.MainPage.DisplayAlert("Ошибка", error, "OK");
                return;
            }

            await Application.Current.MainPage.DisplayAlert("Успешно", "Договор закрыт", "OK");
            await LoadContract(ContractId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContractDetailsViewModel] Failed to close contract");
            await Application.Current.MainPage.DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }

    private static string GetStatusLabel(string status)
    {
        return status switch
        {
            "Draft" => "Черновик",
            "Active" => "Активный",
            "PartiallyPaid" => "Частично оплачен",
            "Paid" => "Оплачен",
            "PartiallyDelivered" => "Частично отгружен",
            "Delivered" => "Отгружен",
            "Closed" => "Закрыт",
            "Cancelled" => "Отменён",
            _ => status
        };
    }

    private static string GetStatusColor(string status)
    {
        return status switch
        {
            "Closed" => "#10B981",
            "Paid" => "#3B82F6",
            "Delivered" => "#8B5CF6",
            "PartiallyPaid" => "#F59E0B",
            "PartiallyDelivered" => "#F59E0B",
            "Cancelled" => "#EF4444",
            _ => "#6B7280"
        };
    }

    public class ContractItemRow
    {
        public int Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public decimal DeliveredQty { get; set; }
        public decimal UnitPrice { get; set; }
        public int? ProductId { get; set; }
        public decimal RemainingQty => Qty - DeliveredQty;
        public bool IsDelivered => DeliveredQty >= Qty;
    }

    public class PaymentRow
    {
        public decimal Amount { get; set; }
        public string Method { get; set; } = string.Empty;
        public DateTime PaidAt { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    public class DeliveryRow
    {
        public int Id { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public DateTime DeliveredAt { get; set; }
        public string Note { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Completed | PendingConversion | Cancelled
        public bool UsedNd40 { get; set; }
        public bool IsPending => string.Equals(Status, "PendingConversion", StringComparison.OrdinalIgnoreCase);
    }

    private class ContractDto
    {
        public int Id { get; set; }
        public string? Type { get; set; }
        public string OrgName { get; set; } = string.Empty;
        public string? Inn { get; set; }
        public string? Phone { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public int TotalItemsCount { get; set; }
        public int DeliveredItemsCount { get; set; }
        public string? Note { get; set; }
        public List<ContractItemDto> Items { get; set; } = new();
        public List<ContractPaymentDto> Payments { get; set; } = new();
        public List<ContractDeliveryDto> Deliveries { get; set; } = new();
        public bool NdWarning { get; set; }
    }

    private class ContractItemDto
    {
        public int Id { get; set; }
        public int? ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public decimal DeliveredQty { get; set; }
        public decimal UnitPrice { get; set; }
    }

    private class ContractPaymentDto
    {
        public decimal Amount { get; set; }
        public string Method { get; set; } = string.Empty;
        public DateTime PaidAt { get; set; }
        public string? Note { get; set; }
    }

    private class ContractDeliveryDto
    {
        public int Id { get; set; }
        public int ContractItemId { get; set; }
        public decimal Qty { get; set; }
        public DateTime DeliveredAt { get; set; }
        public string? Note { get; set; }
        public string? Status { get; set; }
        public bool UsedNd40 { get; set; }
    }
}
