using Microsoft.Maui.Controls;
using System;

namespace ProjectApp.Client.Maui.Views;

public partial class FinancesMenuPage : ContentPage
{
    public FinancesMenuPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
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
}
