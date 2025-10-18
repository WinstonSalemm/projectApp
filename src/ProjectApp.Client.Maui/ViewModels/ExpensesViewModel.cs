using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ExpensesViewModel : ObservableObject
{
    private readonly FinancesApiService _financesApi;

    [ObservableProperty]
    private ObservableCollection<ExpenseItemViewModel> expenses = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private decimal totalAmount;

    [ObservableProperty]
    private int totalCount;

    [ObservableProperty]
    private string? errorMessage;

    public ExpensesViewModel(FinancesApiService financesApi)
    {
        _financesApi = financesApi;
    }

    public async Task LoadExpensesAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            // Load expenses for current month
            var startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);
            
            var expensesDto = await _financesApi.GetExpensesAsync(startDate, endDate);

            Expenses.Clear();
            foreach (var expense in expensesDto.OrderByDescending(e => e.ExpenseDate))
            {
                Expenses.Add(new ExpenseItemViewModel
                {
                    Id = expense.Id,
                    Type = expense.Type,
                    Description = expense.Description,
                    Amount = expense.Amount,
                    ExpenseDate = expense.ExpenseDate,
                    Status = expense.Status,
                    Icon = GetExpenseIcon(expense.Type),
                    StatusColor = expense.Status == "Paid" ? "#4CAF50" : "#FF9800"
                });
            }

            TotalCount = Expenses.Count;
            TotalAmount = Expenses.Sum(e => e.Amount);

            System.Diagnostics.Debug.WriteLine($"[ExpensesViewModel] Loaded {TotalCount} expenses, total: {TotalAmount:N0}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExpensesViewModel] LoadExpensesAsync error: {ex}");
            ErrorMessage = "Ошибка загрузки расходов. Проверьте подключение.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string GetExpenseIcon(string type)
    {
        return type switch
        {
            "Salary" => "💰",
            "Rent" => "🏢",
            "Utilities" => "💡",
            "Customs" => "🛃",
            "Tax" => "📊",
            "Transportation" => "🚚",
            "Marketing" => "📣",
            "Equipment" => "🖥️",
            _ => "📋"
        };
    }
}

public class ExpenseItemViewModel
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Icon { get; set; } = "📋";
    public string StatusColor { get; set; } = "#FF9800";
}
