using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.Models;
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
        if (!Enum.TryParse(paymentType, out PaymentType pt))
        {
            pt = PaymentType.CashWithReceipt;
        }

        Selected = pt;

        if (pt == PaymentType.Return)
        {
            var history = _services.GetRequiredService<SalesHistoryPage>();
            if (history.BindingContext is SalesHistoryViewModel hvm)
            {
                hvm.ShowAll = true;
            }

            await NavigationHelper.PushAsync(history);
            return;
        }

        var auth = _services.GetRequiredService<AuthService>();
        var account = string.IsNullOrWhiteSpace(auth.DisplayName)
            ? auth.UserName ?? "Текущий пользователь"
            : auth.DisplayName;

        async Task OnYes()
        {
            // Initialize sale session with payment type
            var session = _services.GetRequiredService<SaleSession>();
            session.SetPaymentType(pt);
            
            // Open product catalog directly
            var productPage = _services.GetRequiredService<ProductSelectPage>();
            await NavigationHelper.PushAsync(productPage);
        }

        Task OnNo()
        {
            var userSelect = _services.GetRequiredService<UserSelectPage>();
            NavigationHelper.SetRoot(new NavigationPage(userSelect));
            return Task.CompletedTask;
        }

        var audio = _services.GetRequiredService<Plugin.Maui.Audio.IAudioManager>();
        var confirm = new ConfirmAccountPage(audio, account, OnYes, OnNo);
        await NavigationHelper.PushModalAsync(confirm, true);
    }
}
