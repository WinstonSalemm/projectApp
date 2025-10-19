using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.Services;
using System;

namespace ProjectApp.Client.Maui.Views;

public partial class FinancesMenuPage : ContentPage
{
    private readonly AuthService _authService;

    public FinancesMenuPage()
    {
        InitializeComponent();
        _authService = App.Services.GetRequiredService<AuthService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Скрыть "К инкассации" для менеджеров (только для Owner/Admin)
        var isOwner = string.Equals(_authService.Role, "Owner", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(_authService.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        CashCollectionFrame.IsVisible = isOwner;
        
        // TODO: Load total balance from API
        TotalBalanceLabel.Text = "Загрузка...";
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
        // TODO: Navigate to P&L report
        await DisplayAlert("P&L", "Отчет о прибылях и убытках", "OK");
    }

    private async void OnCashFlowClicked(object sender, EventArgs e)
    {
        // TODO: Navigate to Cash Flow report
        await DisplayAlert("Cash Flow", "Отчет о движении денежных средств", "OK");
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
