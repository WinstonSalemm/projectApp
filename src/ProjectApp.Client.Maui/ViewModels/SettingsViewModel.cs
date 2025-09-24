using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using ProjectApp.Client.Maui.Services;
using Microsoft.Maui.Controls;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;

    [ObservableProperty]
    private bool useApi;

    [ObservableProperty]
    private string apiBaseUrl = string.Empty;

    public SettingsViewModel(AppSettings settings)
    {
        _settings = settings;
        // Load current settings
        UseApi = _settings.UseApi;
        ApiBaseUrl = _settings.ApiBaseUrl ?? "http://localhost:5028";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        // Persist to Preferences
        Preferences.Set("UseApi", UseApi);
        Preferences.Set("ApiBaseUrl", ApiBaseUrl ?? "");

        // Update live settings singleton
        _settings.UseApi = UseApi;
        _settings.ApiBaseUrl = string.IsNullOrWhiteSpace(ApiBaseUrl) ? "http://localhost:5028" : ApiBaseUrl;

        // Brief feedback
        await Application.Current!.MainPage!.DisplayAlert("Сохранено", "Настройки применены", "OK");
    }
}
