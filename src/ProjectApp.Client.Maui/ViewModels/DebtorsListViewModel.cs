using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class DebtorsListViewModel : ObservableObject
{
    private readonly DebtorsApiService _debtorsApiService;

    [ObservableProperty]
    private ObservableCollection<DebtorItemViewModel> debtors = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private int totalDebtorsCount;

    [ObservableProperty]
    private decimal totalDebtAmount;

    [ObservableProperty]
    private string? errorMessage;

    public DebtorsListViewModel(DebtorsApiService debtorsApiService)
    {
        _debtorsApiService = debtorsApiService;
    }

    public async Task LoadDebtorsAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            
            // Load from API ✅
            var debtorsDto = await _debtorsApiService.GetDebtorsAsync();
            
            Debtors.Clear();
            foreach (var dto in debtorsDto)
            {
                Debtors.Add(new DebtorItemViewModel
                {
                    ClientId = dto.ClientId,
                    ClientName = dto.ClientName,
                    Phone = dto.Phone ?? string.Empty,
                    TotalDebt = dto.TotalDebt,
                    DebtsCount = dto.DebtsCount,
                    OldestDueDate = dto.OldestDueDate ?? DateTime.Now,
                    IsOverdue = dto.OldestDueDate.HasValue && dto.OldestDueDate.Value < DateTime.Now
                });
            }

            TotalDebtorsCount = Debtors.Count;
            TotalDebtAmount = Debtors.Sum(d => d.TotalDebt);
            
            System.Diagnostics.Debug.WriteLine($"[DebtorsListViewModel] Loaded {TotalDebtorsCount} debtors, total: {TotalDebtAmount:N0}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DebtorsListViewModel] LoadDebtorsAsync error: {ex}");
            ErrorMessage = "Ошибка загрузки должников. Проверьте подключение к интернету.";
            
            // Fallback to mock data if API fails
            if (Debtors.Count == 0)
            {
                Debtors.Add(new DebtorItemViewModel
                {
                    ClientId = 1,
                    ClientName = "Тестовый должник",
                    Phone = "+998901234567",
                    TotalDebt = 750000,
                    DebtsCount = 1,
                    OldestDueDate = DateTime.Now.AddDays(30),
                    IsOverdue = false
                });
                TotalDebtorsCount = 1;
                TotalDebtAmount = 750000;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public class DebtorItemViewModel
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public decimal TotalDebt { get; set; }
    public int DebtsCount { get; set; }
    public DateTime OldestDueDate { get; set; }
    public bool IsOverdue { get; set; }
}
