using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.Models;
using System.Threading.Tasks;
using ProjectApp.Client.Maui.Services;
using ProjectApp.Client.Maui.Views;

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

        if (pt == PaymentType.Return)
        {
            // For returns, open sales history so manager can pick a sale and proceed to Return page
            var history = _services.GetRequiredService<ProjectApp.Client.Maui.Views.SalesHistoryPage>();
            if (history.BindingContext is ProjectApp.Client.Maui.ViewModels.SalesHistoryViewModel hvm)
            {
                // Show all by default for convenience; server will still enforce permissions
                hvm.ShowAll = true;
            }
            await Application.Current!.MainPage!.Navigation.PushAsync(history);
            return;
        }

        // After selecting a payment type (non-return): show full-screen confirmation modal with loud sound
        var auth = _services.GetRequiredService<AuthService>();
        var account = string.IsNullOrWhiteSpace(auth.DisplayName) ? (auth.UserName ?? "аккаунт") : auth.DisplayName;

        // Prepare actions
        async Task OnYes()
        {
            // proceed to category selection
            var startPage = _services.GetRequiredService<SaleStartPage>();
            if (startPage.BindingContext is ProjectApp.Client.Maui.ViewModels.SaleStartViewModel svm2)
                svm2.SelectedPaymentType = pt;
            await Application.Current!.MainPage!.Navigation.PushAsync(startPage);
        }

        async Task OnNo()
        {
            // go back to account selection screen
            var userSelect = _services.GetRequiredService<UserSelectPage>();
            Application.Current!.MainPage = new NavigationPage(userSelect);
            await Task.CompletedTask;
        }

        var audio = _services.GetRequiredService<Plugin.Maui.Audio.IAudioManager>();
        var confirm = new ConfirmAccountPage(audio, account, OnYes, OnNo);
        await Application.Current!.MainPage!.Navigation.PushModalAsync(confirm, true);
    }
}
