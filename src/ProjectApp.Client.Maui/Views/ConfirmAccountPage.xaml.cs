using Microsoft.Maui.Controls;
using Plugin.Maui.Audio;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;

namespace ProjectApp.Client.Maui.Views;

public partial class ConfirmAccountPage : ContentPage
{
    private readonly IAudioManager _audioManager;
    private readonly string _accountName;
    private readonly Func<Task> _onYes;
    private readonly Func<Task> _onNo;

    public ConfirmAccountPage(IAudioManager audioManager, string accountDisplayName, Func<Task> onYes, Func<Task> onNo)
    {
        InitializeComponent();
        _audioManager = audioManager;
        _accountName = accountDisplayName;
        _onYes = onYes;
        _onNo = onNo;
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
        try { if (_onYes != null) await _onYes(); } catch { }
        try { await Navigation.PopModalAsync(); } catch { }
    }

    private async void OnNoClicked(object? sender, EventArgs e)
    {
        try { if (_onNo != null) await _onNo(); } catch { }
        try { await Navigation.PopModalAsync(); } catch { }
    }
}

