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
        // Специальная обработка для договоров
        if (paymentType == "Contract")
        {
            var contractsPage = _services.GetRequiredService<ContractsPage>();
            if (contractsPage.BindingContext is ContractsViewModel vm)
            {
                await vm.LoadContractsCommand.ExecuteAsync(null);
            }
            await NavigationHelper.PushAsync(contractsPage);
            return;
        }

        // Специальная обработка для долгов
        if (paymentType == "Debt")
        {
            var debtorsPage = _services.GetRequiredService<DebtorsListPage>();
            await NavigationHelper.PushAsync(debtorsPage);
            return;
        }

        if (!Enum.TryParse(paymentType, out PaymentType pt))
        {
            pt = PaymentType.CashWithReceipt;
        }

        Selected = pt;

        if (pt == PaymentType.Return)
        {
            var returnSourcePage = _services.GetRequiredService<ReturnSourceSelectorPage>();
            await NavigationHelper.PushAsync(returnSourcePage);
            return;
        }

        var auth = _services.GetRequiredService<AuthService>();
        var account = string.IsNullOrWhiteSpace(auth.DisplayName)
            ? auth.UserName ?? "Текущий пользователь"
            : auth.DisplayName;

        try
        {
            var audio = _services.GetRequiredService<Plugin.Maui.Audio.IAudioManager>();
            var confirm = new ConfirmAccountPage(audio, account);
            
            // Показываем модальное окно и ждем результат
            await NavigationHelper.PushModalAsync(confirm, true);
            var confirmed = await confirm.Result;
            
            System.Diagnostics.Debug.WriteLine($"[PaymentSelect] Confirmed: {confirmed}");
            
            if (confirmed)
            {
                // ДА - инициализируем сессию и открываем каталог товаров
                System.Diagnostics.Debug.WriteLine("[PaymentSelect] User confirmed, initializing session");
                var session = _services.GetRequiredService<SaleSession>();
                session.SetPaymentType(pt);
                
                System.Diagnostics.Debug.WriteLine("[PaymentSelect] Getting ProductSelectPage");
                var productPage = _services.GetRequiredService<ProductSelectPage>();
                
                System.Diagnostics.Debug.WriteLine("[PaymentSelect] Pushing ProductSelectPage");
                await NavigationHelper.PushAsync(productPage);
                System.Diagnostics.Debug.WriteLine("[PaymentSelect] ProductSelectPage pushed successfully");
            }
            else
            {
                // НЕТ - выходим из аккаунта и возвращаемся к выбору пользователя
                System.Diagnostics.Debug.WriteLine("[PaymentSelect] User declined, logging out");
                auth.Logout();
                var userSelect = _services.GetRequiredService<UserSelectPage>();
                NavigationHelper.SetRoot(new NavigationPage(userSelect));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PaymentSelect] ERROR: {ex}");
            await NavigationHelper.DisplayAlert("Ошибка", $"Произошла ошибка: {ex.Message}\n\n{ex.StackTrace}", "OK");
        }
    }
}
