using System;

using System.Collections.Generic;

using System.Collections.ObjectModel;

using System.Linq;

using System.Net.Http.Json;

using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using Microsoft.Maui.ApplicationModel;

using ProjectApp.Client.Maui.Models;

using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class SaleStartViewModel : ObservableObject

{
    private const string AllCategoriesLabel = "Все категории";

    private readonly ICatalogService _catalog;

    private readonly AuthService _authService;

    private readonly ILogger<SaleStartViewModel> _logger;

    private readonly SaleSession _session;

        
    public ObservableCollection<CategoryDto> Categories { get; } = new();

    public ObservableCollection<UserDto> Managers { get; } = new();

    public ObservableCollection<StoreOption> Stores { get; } = new();

    public ObservableCollection<SaleMethodOption> SaleMethods { get; } = new();

    [ObservableProperty]

    private CategoryDto? selectedCategory;

    [ObservableProperty]

    private UserDto? selectedManager;

    [ObservableProperty]

    private StoreOption? selectedStore;

    [ObservableProperty]

    private SaleMethodOption? selectedSaleMethod;

    [ObservableProperty]

    private PaymentType selectedPaymentType = PaymentType.CashWithReceipt;

    [ObservableProperty]

    private bool isCategoriesLoading;

    [ObservableProperty]

    private bool isCategoriesError;

    [ObservableProperty]

    private bool isManagersLoading;

    [ObservableProperty]

    private bool isStoresLoading;

    [ObservableProperty]

    private bool isSaleMethodsLoading;

    [ObservableProperty]

    private string? categoriesErrorMessage;

    [ObservableProperty]

    private bool showCategoriesEmptyState;

    [ObservableProperty]

    private bool canSelectSaleMethods;

    [ObservableProperty]

    private string? stepValidationMessage;

    [ObservableProperty]

    private bool hasStepValidationMessage;

    [ObservableProperty]

    private bool showCategoriesSection;

    public SaleStartViewModel(ICatalogService catalog, AuthService authService, ILogger<SaleStartViewModel> logger, SaleSession session)
    {
        _catalog = catalog;
        _authService = authService;
        _logger = logger;
        _session = session;
        SeedSaleMethods();
        _ = InitialiseAsync();
    }

    private void SeedSaleMethods()
    {
        if (SaleMethods.Count > 0)
            return;

        IsSaleMethodsLoading = true;
        try
        {
            var methods = new[]
            {
                new SaleMethodOption { Id = SaleMethodKind.CashWithReceipt, Title = "Наличными с чеком", Description = "Стандартная продажа с выдачей фискального чека.", Icon = "$", PaymentType = PaymentType.CashWithReceipt },
                new SaleMethodOption { Id = SaleMethodKind.CashNoReceipt,   Title = "Наличными без чека", Description = "Продажа за наличные без печати чека.",            Icon = "$", PaymentType = PaymentType.CashNoReceipt },
                new SaleMethodOption { Id = SaleMethodKind.CardWithReceipt,  Title = "Картой с чеком",    Description = "Оплата банковской картой с фискальным чеком.",     Icon = "C", PaymentType = PaymentType.CardWithReceipt },
                new SaleMethodOption { Id = SaleMethodKind.ClickWithReceipt, Title = "Click с чеком",     Description = "Онлайн-оплата Click с чеком.",                      Icon = "K", PaymentType = PaymentType.ClickWithReceipt },
                new SaleMethodOption { Id = SaleMethodKind.ClickNoReceipt,   Title = "Click без чека",    Description = "Click-оплата с ручным учётом чека.",                 Icon = "K", PaymentType = PaymentType.ClickNoReceipt },
                new SaleMethodOption { Id = SaleMethodKind.Site,             Title = "Сайт",              Description = "Продажа, оформленная через интернет-магазин.",       Icon = "W", PaymentType = PaymentType.Site, IsEnabled = false },
                new SaleMethodOption { Id = SaleMethodKind.Return,           Title = "Возврат",           Description = "Перейти к оформлению возврата.",                    Icon = "R", PaymentType = PaymentType.Return },
                new SaleMethodOption { Id = SaleMethodKind.Reservation,      Title = "Бронь",             Description = "Создать бронь и удержать товар.",                    Icon = "B", PaymentType = PaymentType.Reservation },
                new SaleMethodOption { Id = SaleMethodKind.Payme,            Title = "Payme",             Description = "Онлайн-оплата через Payme.",                         Icon = "P", PaymentType = PaymentType.Payme, IsEnabled = false },
                new SaleMethodOption { Id = SaleMethodKind.Contract,         Title = "Договор",           Description = "Продажа по договору или предоплате.",                Icon = "D", PaymentType = PaymentType.Contract },
                new SaleMethodOption { Id = SaleMethodKind.CommissionClients,Title = "Комиссионные клиенты", Description = "Открыть клиентов, закреплённых за менеджером.",  Icon = "U", PaymentType = null }
            };

            foreach (var m in methods)
                SaleMethods.Add(m);
        }
        finally
        {
            IsSaleMethodsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadCategoriesAsync()
    {
        if (IsCategoriesLoading)
            return;

        try
        {
            IsCategoriesLoading = true;
            IsCategoriesError = false;
            CategoriesErrorMessage = null;

            _logger.LogInformation("[SaleStartViewModel] LoadCategoriesAsync started");
            var raw = await _catalog.GetCategoriesAsync();
            _logger.LogInformation("[SaleStartViewModel] LoadCategoriesAsync received {Count} categories", raw?.Count() ?? 0);
            var list = raw?
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .Select(name => new CategoryDto { Name = name })
                .ToList()
                ?? new List<CategoryDto>();

            if (list.Count > 0 && list.All(c => !string.Equals(c.Name, AllCategoriesLabel, StringComparison.OrdinalIgnoreCase)))
            {
                list.Insert(0, new CategoryDto { Name = AllCategoriesLabel });
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Categories.Clear();
                foreach (var item in list)
                    Categories.Add(item);

                ShowCategoriesEmptyState = Categories.Count == 0;
                ShowCategoriesSection = Categories.Count > 0;
                if (!ShowCategoriesEmptyState && SelectedCategory is null)
                    SelectedCategory = Categories.FirstOrDefault();
            });
        }
        catch (Exception ex)
        {
            IsCategoriesError = true;
            CategoriesErrorMessage = $"Ошибка загрузки категорий: {ex.Message}";
            ShowCategoriesEmptyState = true;
            ShowCategoriesSection = false;
            _logger.LogError(ex, "[SaleStartViewModel] Failed to load categories. Error: {ErrorMessage}", ex.Message);
        }
        finally
        {
            IsCategoriesLoading = false;
        }
    }
    [RelayCommand]
    private async Task LoadManagersAsync()
    {
        if (IsManagersLoading)
        {
            return;
        }
        try
        {
            IsManagersLoading = true;
            
            // Загружаем реальных пользователей из API
            var client = new System.Net.Http.HttpClient 
            { 
                BaseAddress = new Uri("https://tranquil-upliftment-production.up.railway.app") 
            };
            
            var response = await client.GetAsync("/api/users");
            List<UserDto> userList = new();
            
            if (response.IsSuccessStatusCode)
            {
                var apiUsers = await response.Content.ReadFromJsonAsync<List<ApiUserDto>>();
                userList = apiUsers?.Where(u => u.IsActive)
                    .Select(u => new UserDto 
                    { 
                        Id = u.UserName ?? "", 
                        DisplayName = u.DisplayName ?? u.UserName ?? "" 
                    })
                    .ToList() ?? new List<UserDto>();
            }
            
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Managers.Clear();
                foreach (var user in userList)
                {
                    Managers.Add(user);
                }
                
                var preselected = Managers.FirstOrDefault(m =>
                    !string.IsNullOrWhiteSpace(_authService.UserName) &&
                    string.Equals(m.Id, _authService.UserName, StringComparison.OrdinalIgnoreCase));
                SelectedManager = preselected ?? Managers.FirstOrDefault();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load managers from API");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Managers.Clear();
                SelectedManager = null;
            });
        }
        finally
        {
            IsManagersLoading = false;
            UpdateStepState();
        }
    }
    
    private class ApiUserDto
    {
        public int Id { get; set; }
        public string? UserName { get; set; }
        public string? DisplayName { get; set; }
        public string? Role { get; set; }
        public bool IsActive { get; set; }
    }

private async Task LoadStoresAsync()
{
    if (IsStoresLoading)
    {
        return;
    }

    try
    {
        IsStoresLoading = true;
        var defaults = GetDefaultStores();
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Stores.Clear();
            foreach (var store in defaults)
            {
                Stores.Add(store);
            }

            if (Stores.Count > 0 && SelectedStore is null)
            {
                SelectedStore = Stores.FirstOrDefault(s => !s.IsAdmin) ?? Stores.First();
            }
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to load stores");
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Stores.Clear();
            SelectedStore = null;
        });
    }
    finally
    {
        IsStoresLoading = false;
        UpdateStepState();
    }
}

public void ApplySaleMethod(SaleMethodOption option)
{
    if (option is null)
    {
        return;
    }

    SelectedSaleMethod = option;

    if (option.PaymentType.HasValue)
    {
        SelectedPaymentType = option.PaymentType.Value;
        _session.SetPaymentType(option.PaymentType.Value);
    }
}

private async Task InitialiseAsync()
{
    try
    {
        _session.Reset();
        await Task.WhenAll(
            LoadCategoriesAsync(),
            LoadManagersAsync(),
            LoadStoresAsync());
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "InitialiseAsync failed");
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Services.NavigationHelper.DisplayAlert("Ошибка инициализации", ex.Message, "OK");
        });
    }
}

private void UpdateStepState()
{
    var isReady = SelectedManager is not null && SelectedStore is not null;
    CanSelectSaleMethods = isReady;
    StepValidationMessage = isReady ? null : "Выберите менеджера и точку продаж, чтобы продолжить.";
    HasStepValidationMessage = !string.IsNullOrWhiteSpace(StepValidationMessage);

    if (isReady)
    {
        _session.SetContext(SelectedManager!, SelectedStore!);
    }
    else
    {
        _session.Reset();
    }
}

private static string? ExtractCorrelationId(string? message)
{
    if (string.IsNullOrWhiteSpace(message))
    {
        return null;
    }

    const string marker = "correlationId";
    var idx = message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
    if (idx < 0)
    {
        return null;
    }

    return message[idx..].Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault();
}

private IReadOnlyList<StoreOption> GetDefaultStores()
{
    var stores = new List<StoreOption>
    {
        new() { Id = "store-main", Name = "Магазин" }
    };

    if (string.Equals(_authService.Role, "Admin", StringComparison.OrdinalIgnoreCase))
    {
        stores.Insert(0, new StoreOption { Id = "admin", Name = "ADMIN", IsAdmin = true });
    }

    return stores;
}

partial void OnSelectedManagerChanged(UserDto? value)
{
    UpdateStepState();
}

partial void OnSelectedStoreChanged(StoreOption? value)
{
    UpdateStepState();
}
}
