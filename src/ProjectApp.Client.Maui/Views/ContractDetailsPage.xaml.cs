using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class ContractDetailsPage : ContentPage
{
    private readonly ContractDetailsViewModel _vm;

    public ContractDetailsPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<ContractDetailsViewModel>();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Получаем contractId из query параметров или свойства
        if (BindingContext is ContractDetailsViewModel vm && vm.ContractId > 0)
        {
            await vm.LoadContractCommand.ExecuteAsync(vm.ContractId);
        }
    }
}
