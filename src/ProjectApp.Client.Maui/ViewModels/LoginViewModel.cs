using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.Services;
using System.Threading.Tasks;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private string userName = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private bool isPasswordVisible = true; // покажем поле, но оно опционально

    public LoginViewModel(AuthService auth, IServiceProvider services)
    {
        _auth = auth;
        _services = services;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        // Если пароль пустой — отправим null (для менеджера)
        var ok = await _auth.LoginAsync(UserName, string.IsNullOrWhiteSpace(Password) ? null : Password);
        if (!ok)
        {
            await Application.Current!.MainPage!.DisplayAlert("Ошибка", "Не удалось войти", "OK");
            return;
        }
        // Роутинг по роли
        if (string.Equals(_auth.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            var admin = _services.GetRequiredService<ProjectApp.Client.Maui.Views.AdminDashboardPage>();
            Application.Current!.MainPage = new NavigationPage(admin);
        }
        else
        {
            var pay = _services.GetRequiredService<ProjectApp.Client.Maui.Views.PaymentSelectPage>();
            Application.Current!.MainPage = new NavigationPage(pay);
        }
    }
}
