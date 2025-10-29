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
            var contracts = await _contractsService.GetContractsAsync();
            
            _allContracts.Clear();
            foreach (var c in contracts)
            {
                _allContracts.Add(new ContractItemViewModel
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
            
            ApplyFilter();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Ошибка", $"Не удалось загрузить договора: {ex.Message}", "OK");
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
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Contracts.Clear();
        var filtered = _allContracts.Where(c => c.Type == SelectedType).ToList();
        foreach (var contract in filtered)
        {
            Contracts.Add(contract);
        }
        HasContracts = Contracts.Count > 0;
    }

    [RelayCommand]
    private async Task CreateContract()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("CreateContract: Starting...");
            var page = _services.GetRequiredService<ProjectApp.Client.Maui.Views.ContractCreatePage>();
            System.Diagnostics.Debug.WriteLine("CreateContract: Page created");
            await Shell.Current.Navigation.PushAsync(page);
            System.Diagnostics.Debug.WriteLine("CreateContract: Navigation complete");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateContract ERROR: {ex.Message}\n{ex.StackTrace}");
            await Shell.Current.DisplayAlert("Ошибка", $"Не удалось открыть форму создания:\n{ex.Message}", "OK");
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

            await Shell.Current.DisplayAlert(
                $"Договор {item.ContractNumber}",
                $"Тип: {item.Type}\n" +
                $"Статус: {item.Status}\n" +
                $"Сумма: {item.TotalAmount:N0}\n" +
                $"Оплачено: {item.PaidPercent:F0}%\n" +
                $"Отгружено: {item.ShippedPercent:F0}%\n" +
                $"Долг: {item.BalanceDue:N0}",
                "OK"
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ViewContract error: {ex.Message}\n{ex.StackTrace}");
            try
            {
                await Shell.Current.DisplayAlert("Ошибка", $"Не удалось открыть договор: {ex.Message}", "OK");
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

    public string StatusColor => Status switch
    {
        "Active" => "#4CAF50",
        "Closed" => "#9E9E9E",
        "Cancelled" => "#F44336",
        _ => "#2196F3"
    };
}
