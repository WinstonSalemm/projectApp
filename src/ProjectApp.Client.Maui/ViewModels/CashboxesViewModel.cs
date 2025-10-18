using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class CashboxesViewModel : ObservableObject
{
    private readonly FinancesApiService _financesApi;

    [ObservableProperty]
    private ObservableCollection<CashboxItemViewModel> cashboxes = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private decimal totalBalance;

    [ObservableProperty]
    private string? errorMessage;

    public CashboxesViewModel(FinancesApiService financesApi)
    {
        _financesApi = financesApi;
    }

    public async Task LoadCashboxesAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var balances = await _financesApi.GetCashboxBalancesAsync();

            Cashboxes.Clear();
            foreach (var balance in balances)
            {
                Cashboxes.Add(new CashboxItemViewModel
                {
                    Id = balance.CashboxId,
                    Name = balance.CashboxName,
                    Type = balance.Type,
                    Balance = balance.Balance,
                    Currency = balance.Currency,
                    Icon = GetCashboxIcon(balance.Type)
                });
            }

            TotalBalance = Cashboxes.Sum(c => c.Balance);

            System.Diagnostics.Debug.WriteLine($"[CashboxesViewModel] Loaded {Cashboxes.Count} cashboxes, total: {TotalBalance:N0}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CashboxesViewModel] LoadCashboxesAsync error: {ex}");
            ErrorMessage = "Ошибка загрузки касс. Проверьте подключение.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string GetCashboxIcon(string type)
    {
        return type switch
        {
            "Office" => "🏢",
            "Warehouse" => "📦",
            "Manager" => "👤",
            "BankAccount" => "🏦",
            "CryptoWallet" => "₿",
            _ => "💰"
        };
    }
}

public class CashboxItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "UZS";
    public string Icon { get; set; } = "💰";
}
