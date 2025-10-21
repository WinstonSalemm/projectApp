using Microsoft.Maui.Controls;
using Plugin.Maui.Audio;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;

namespace ProjectApp.Client.Maui.Views;

public partial class ConfirmAccountPage : ContentPage
{
    private readonly IAudioManager _audioManager;
    private readonly string _accountName;
    private readonly TaskCompletionSource<bool> _resultSource = new();

    public Task<bool> Result => _resultSource.Task;

    public ConfirmAccountPage(IAudioManager audioManager, string accountDisplayName)
    {
        InitializeComponent();
        _audioManager = audioManager;
        _accountName = accountDisplayName;
        SubtitleLabel.Text = $"\"{_accountName}\"?";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Play loud alert sound
        try
        {
            // Try root-linked asset pig.mp3 (packaged as Resources/Raw/pig.mp3)
            Stream s;
            try { s = await FileSystem.OpenAppPackageFileAsync("pig.mp3"); }
            catch { s = await FileSystem.OpenAppPackageFileAsync("Resources/Raw/pig.mp3"); }
            var player = _audioManager.CreatePlayer(s);
            player.Volume = 1.0; // max
            player.Play();
        }
        catch
        {
            // Fallback: TTS loud phrase
            try { await TextToSpeech.SpeakAsync("Внимание! Подтверждение аккаунта.", new SpeechOptions { Volume = 1.0f }); } catch { }
        }
    }

    private async void OnYesClicked(object? sender, EventArgs e)
    {
        _resultSource.TrySetResult(true);
        await Navigation.PopModalAsync();
    }

    private async void OnNoClicked(object? sender, EventArgs e)
    {
        _resultSource.TrySetResult(false);
        await Navigation.PopModalAsync();
    }
}

