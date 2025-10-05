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
        // Go to category selection page next
        var start = _services.GetRequiredService<ProjectApp.Client.Maui.Views.SaleStartPage>();
        if (start.BindingContext is ProjectApp.Client.Maui.ViewModels.SaleStartViewModel svm)
        {
            svm.SelectedPaymentType = pt;
        }
        await Application.Current!.MainPage!.Navigation.PushAsync(start);
    }
}
