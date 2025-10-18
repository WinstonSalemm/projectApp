using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ProjectApp.Client.Maui.Services;
using System.Collections.ObjectModel;
using System.Net.Http.Json;

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
    [ObservableProperty] private decimal totalAmount;
    [ObservableProperty] private decimal paidAmount;
    [ObservableProperty] private int totalItemsCount;
    [ObservableProperty] private int deliveredItemsCount;
    [ObservableProperty] private string note = string.Empty;

    public ObservableCollection<ContractItemRow> Items { get; } = new();
    public ObservableCollection<PaymentRow> Payments { get; } = new();
    public ObservableCollection<DeliveryRow> Deliveries { get; } = new();

    // UI свойства
    public decimal RemainingAmount => TotalAmount - PaidAmount;
    public bool IsPaid => PaidAmount >= TotalAmount;
    public bool IsFullyDelivered => DeliveredItemsCount >= TotalItemsCount;
    public bool CanClose => IsPaid && IsFullyDelivered && Status != "Closed";
    public string StatusLabel => GetStatusLabel(Status);

    public ContractDetailsViewModel(IHttpClientFactory httpFactory, ILogger<ContractDetailsViewModel> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    [RelayCommand]
    public async Task LoadContract(int id)
    {
        try
        {
            IsLoading = true;
            ContractId = id;

            var client = _httpFactory.CreateClient("api");
            var response = await client.GetAsync($"/api/contracts/{id}");

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
                TotalAmount = contract.TotalAmount;
                PaidAmount = contract.PaidAmount;
                TotalItemsCount = contract.TotalItemsCount;
                DeliveredItemsCount = contract.DeliveredItemsCount;
                Note = contract.Note ?? "";

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
                        ItemName = Items.FirstOrDefault(i => i.Id == delivery.ContractItemId)?.Name ?? "",
                        Qty = delivery.Qty,
                        DeliveredAt = delivery.DeliveredAt,
                        Note = delivery.Note ?? ""
                    });
                }

                OnPropertyChanged(nameof(RemainingAmount));
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
            var client = _httpFactory.CreateClient("api");
            var dto = new { Amount = amount, Method = "BankTransfer", Note = (string?)null };
            var response = await client.PostAsJsonAsync($"/api/contracts/{ContractId}/payments", dto);

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
            var client = _httpFactory.CreateClient("api");
            var dto = new { ContractItemId = item.Id, Qty = qty, Note = (string?)null };
            var response = await client.PostAsJsonAsync($"/api/contracts/{ContractId}/deliveries", dto);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                await Application.Current.MainPage.DisplayAlert("Ошибка", error, "OK");
                return;
            }

            await Application.Current.MainPage.DisplayAlert("Успешно", "Товар отгружен", "OK");
            await LoadContract(ContractId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContractDetailsViewModel] Failed to deliver item");
            await Application.Current.MainPage.DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task AddItem()
    {
        // Открываем диалог выбора товара
        var productPage = App.Services.GetRequiredService<Views.ProductSelectPage>();
        await Shell.Current.Navigation.PushAsync(productPage);
        
        // После выбора товара добавляем в договор
        // TODO: реализовать выбор товара и добавление
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
            var client = _httpFactory.CreateClient("api");
            var response = await client.DeleteAsync($"/api/contracts/{ContractId}/items/{item.Id}");

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
            var client = _httpFactory.CreateClient("api");
            var response = await client.PostAsync($"/api/contracts/{ContractId}/close", null);

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
        public string ItemName { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public DateTime DeliveredAt { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    private class ContractDto
    {
        public int Id { get; set; }
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
        public int ContractItemId { get; set; }
        public decimal Qty { get; set; }
        public DateTime DeliveredAt { get; set; }
        public string? Note { get; set; }
    }
}
