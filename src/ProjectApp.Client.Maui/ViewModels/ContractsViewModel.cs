using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ContractsViewModel : ObservableObject
{
    private readonly IContractsService _contractsService;
    private readonly IServiceProvider _services;
    private List<ContractItemViewModel> _allContracts = new();

    [ObservableProperty]
    private ObservableCollection<ContractItemViewModel> _contracts = new();

    [ObservableProperty]
    private ContractItemViewModel? _selectedContract;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _selectedType = "Open"; // Open or Closed

    [ObservableProperty]
    private bool _hasContracts;

    public ContractsViewModel(IContractsService contractsService, IServiceProvider services)
    {
        _contractsService = contractsService;
        _services = services;
    }

    [RelayCommand]
    private async Task LoadContracts()
    {
        try
        {
            IsLoading = true;
            Contracts.Clear();
            var contracts = await _contractsService.GetByKindAsync(SelectedType);
            foreach (var c in contracts)
            {
                Contracts.Add(new ContractItemViewModel
                {
                    Id = c.Id,
                    Type = c.Type,
                    ContractNumber = c.ContractNumber ?? $"CONTRACT-{c.Id}",
                    ClientName = c.OrgName,
                    Status = c.Status,
                    TotalAmount = c.TotalAmount,
                    PaidAmount = c.PaidAmount,
                    ShippedAmount = c.ShippedAmount,
                    PaidPercent = c.PaidPercent,
                    ShippedPercent = c.ShippedPercent,
                    BalanceDue = c.BalanceDue,
                    CreatedAt = c.CreatedAt
                });
            }
            HasContracts = Contracts.Count > 0;
        }
        catch (Exception ex)
        {
            await NavigationHelper.DisplayAlert("Ошибка", $"Не удалось загрузить договора: {ex.Message}", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectType(string type)
    {
        SelectedType = type;
        _ = LoadContracts();
    }

    private void ApplyFilter() { }

    [RelayCommand]
    private async Task CreateContract()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("CreateContract: Starting...");
            var page = _services.GetRequiredService<ProjectApp.Client.Maui.Views.ContractCreatePage>();
            System.Diagnostics.Debug.WriteLine("CreateContract: Page created");
            await NavigationHelper.PushAsync(page);
            System.Diagnostics.Debug.WriteLine("CreateContract: Navigation complete");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateContract ERROR: {ex.Message}\n{ex.StackTrace}");
            await NavigationHelper.DisplayAlert("Ошибка", $"Не удалось открыть форму создания:\n{ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task ViewContract(ContractItemViewModel? contract)
    {
        try
        {
            var item = contract ?? SelectedContract;
            if (item == null) 
            {
                System.Diagnostics.Debug.WriteLine("ViewContract: contract is null");
                return;
            }

            var detailsPage = _services.GetRequiredService<Views.ContractDetailsPage>();
            if (detailsPage.BindingContext is ContractDetailsViewModel vm)
            {
                vm.ContractId = item.Id;
            }
            await NavigationHelper.PushAsync(detailsPage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ViewContract error: {ex.Message}\n{ex.StackTrace}");
            try
            {
                await NavigationHelper.DisplayAlert("Ошибка", $"Не удалось открыть договор: {ex.Message}", "OK");
            }
            catch
            {
                // Даже диалог не открылся
            }
        }
    }
}

public partial class ContractItemViewModel : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _type = string.Empty;

    [ObservableProperty]
    private string? _contractNumber;

    [ObservableProperty]
    private string _clientName = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private decimal _totalAmount;

    [ObservableProperty]
    private decimal _paidAmount;

    [ObservableProperty]
    private decimal _shippedAmount;

    [ObservableProperty]
    private decimal _paidPercent;

    [ObservableProperty]
    private decimal _shippedPercent;

    [ObservableProperty]
    private decimal _balanceDue;

    [ObservableProperty]
    private DateTime _createdAt;

    public double PaidProgress => (double)PaidPercent / 100.0;
    public double ShippedProgress => (double)ShippedPercent / 100.0;
    public bool HasDebt => BalanceDue > 0;

    public bool IsOpen => string.Equals(Type?.Trim(), "Open", StringComparison.OrdinalIgnoreCase);
    public decimal ItemsTotalForCard => IsOpen ? ShippedAmount : TotalAmount;
    public decimal RemainingForCard => IsOpen ? (TotalAmount - ShippedAmount) : BalanceDue;
    public string RemainingColor
    {
        get
        {
            if (IsOpen)
            {
                if (TotalAmount <= 0) return "#16A34A"; // green
                var ratio = RemainingForCard / TotalAmount; // доля остатка лимита
                if (ratio <= 0.10m) return "#EF4444"; // red
                if (ratio <= 0.25m) return "#F59E0B"; // orange
                return "#16A34A"; // green
            }
            // Closed: остаток = долг
            return RemainingForCard > 0 ? "#EF4444" : "#16A34A";
        }
    }

    public string StatusColor => Status switch
    {
        "Active" => "#4CAF50",
        "Closed" => "#9E9E9E",
        "Cancelled" => "#F44336",
        _ => "#2196F3"
    };
}
