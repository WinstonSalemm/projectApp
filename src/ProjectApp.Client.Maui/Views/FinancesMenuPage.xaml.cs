using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.Services;
using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProjectApp.Client.Maui.Views;

public partial class FinancesMenuPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly ApiService _apiService;

    // Конструктор для DI (используется в SimpleAdminPage)
    public FinancesMenuPage(AuthService authService, ApiService apiService)
    {
        InitializeComponent();
        _authService = authService;
        _apiService = apiService;
    }

    // Конструктор для DataTemplate в AppShell
    public FinancesMenuPage() : this(
        App.Services.GetRequiredService<AuthService>(),
        App.Services.GetRequiredService<ApiService>())
    {
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Скрыть "К инкассации" для менеджеров (только для Owner/Admin)
        var isOwner = string.Equals(_authService.Role, "Owner", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(_authService.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        CashCollectionFrame.IsVisible = isOwner;
        
        // Загрузить баланс касс
        await LoadTotalBalanceAsync();
    }

    private async Task LoadTotalBalanceAsync()
    {
        try
        {
            TotalBalanceLabel.Text = "Загрузка...";
            
            var response = await _apiService.GetAsync<List<CashboxDto>>("api/cashboxes?includeInactive=false");
            
            if (response != null && response.Any())
            {
                var total = response.Sum(c => c.Balance);
                TotalBalanceLabel.Text = $"{total:N0} сум";
            }
            else
            {
                TotalBalanceLabel.Text = "0 сум";
            }
        }
        catch (HttpRequestException ex)
        {
            TotalBalanceLabel.Text = "Ошибка загрузки";
            System.Diagnostics.Debug.WriteLine($"[FinancesMenu] HTTP Error loading balance: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[FinancesMenu] StatusCode: {ex.StatusCode}");
        }
        catch (Exception ex)
        {
            TotalBalanceLabel.Text = "Ошибка загрузки";
            System.Diagnostics.Debug.WriteLine($"[FinancesMenu] Error loading balance: {ex.GetType().Name} - {ex.Message}");
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"[FinancesMenu] Inner: {ex.InnerException.Message}");
            }
        }
    }

    private class CashboxDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public bool IsActive { get; set; }
    }

    private async void OnCashboxesClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("finances/cashboxes");
    }

    private async void OnTransactionsClicked(object sender, EventArgs e)
    {
        // TODO: Navigate to transactions page
        await DisplayAlert("Транзакции", "История транзакций", "OK");
    }

    private async void OnExpensesClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("finances/expenses");
    }

    private async void OnPLReportClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("analytics/finance");
    }

    private async void OnCashFlowClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("analytics/finance");
    }

    private async void OnCashCollectionClicked(object sender, EventArgs e)
    {
        try
        {
            var page = App.Services.GetRequiredService<CashCollectionPage>();
            await Navigation.PushAsync(page);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", $"Не удалось открыть страницу: {ex.Message}", "OK");
        }
    }
}
