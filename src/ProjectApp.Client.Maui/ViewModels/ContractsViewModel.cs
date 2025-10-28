using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ContractsViewModel : ObservableObject
{
    private readonly IContractsService _contractsService;

    [ObservableProperty]
    private ObservableCollection<ContractItemViewModel> _contracts = new();

    [ObservableProperty]
    private ContractItemViewModel? _selectedContract;

    [ObservableProperty]
    private bool _isLoading;

    public ContractsViewModel(IContractsService contractsService)
    {
        _contractsService = contractsService;
    }

    [RelayCommand]
    private async Task LoadContracts()
    {
        try
        {
            IsLoading = true;
            var contracts = await _contractsService.GetContractsAsync();
            
            Contracts.Clear();
            foreach (var c in contracts)
            {
                Contracts.Add(new ContractItemViewModel
                {
                    Id = c.Id,
                    Type = c.Type,
                    ContractNumber = c.ContractNumber,
                    ClientName = c.OrgName, // TODO: load from Clients
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
    private async Task CreateContract()
    {
        await Shell.Current.DisplayAlert("В разработке", "Создание договоров будет реализовано в следующей версии", "OK");
    }

    [RelayCommand]
    private async Task ViewContract()
    {
        if (SelectedContract == null) return;

        await Shell.Current.DisplayAlert(
            $"Договор {SelectedContract.ContractNumber}",
            $"Тип: {SelectedContract.Type}\n" +
            $"Статус: {SelectedContract.Status}\n" +
            $"Сумма: {SelectedContract.TotalAmount:N0}\n" +
            $"Оплачено: {SelectedContract.PaidPercent:F0}%\n" +
            $"Отгружено: {SelectedContract.ShippedPercent:F0}%\n" +
            $"Долг: {SelectedContract.BalanceDue:N0}",
            "OK"
        );
    }
}

public partial class ContractItemViewModel : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _type = string.Empty;

    [ObservableProperty]
    private string _contractNumber = string.Empty;

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
