using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.Models;
using System.Threading.Tasks;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class PaymentSelectViewModel : ObservableObject
{
    [ObservableProperty]
    private PaymentType selected = PaymentType.CashWithReceipt;

    private readonly IServiceProvider _services;

    public PaymentSelectViewModel(IServiceProvider services)
    {
        _services = services;
    }

    [RelayCommand]
    private async Task SelectAsync(string paymentType)
    {
        if (!System.Enum.TryParse<PaymentType>(paymentType, out var pt))
            pt = PaymentType.CashWithReceipt;
        Selected = pt;
        var qs = _services.GetRequiredService<ProjectApp.Client.Maui.Views.QuickSalePage>();
        if (qs.BindingContext is ProjectApp.Client.Maui.ViewModels.QuickSaleViewModel vm)
        {
            vm.SelectedPaymentType = pt;
        }
        await Application.Current!.MainPage!.Navigation.PushAsync(qs);
    }
}
