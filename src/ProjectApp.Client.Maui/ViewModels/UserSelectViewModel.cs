using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.Services;
using System.Threading.Tasks;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class UserSelectViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly IServiceProvider _services;

    public UserSelectViewModel(AuthService auth, IServiceProvider services)
    {
        _auth = auth;
        _services = services;
    }

    [RelayCommand]
    private async Task LoginManagerAsync(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return;
        var ok = await _auth.LoginAsync(userName, null);
        if (!ok)
        {
            await Application.Current!.MainPage!.DisplayAlert("Ошибка", $"Не удалось войти: {userName}", "OK");
            return;
        }
        var qs = _services.GetRequiredService<ProjectApp.Client.Maui.Views.QuickSalePage>();
        // Reset navigation to QuickSale
        Application.Current!.MainPage = new NavigationPage(qs);
    }

    [RelayCommand]
    private Task LoginShopAsync() => LoginManagerAsync("shop");

    [RelayCommand]
    private async Task LoginAdminAsync()
    {
        // Navigate to LoginPage with pre-filled admin username
        var login = _services.GetRequiredService<ProjectApp.Client.Maui.Views.LoginPage>();
        if (login.BindingContext is ProjectApp.Client.Maui.ViewModels.LoginViewModel lvm)
        {
            lvm.UserName = "admin";
            lvm.IsPasswordVisible = true;
        }
        await Application.Current!.MainPage!.Navigation.PushAsync(login);
    }
}
