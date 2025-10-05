using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.Models;
using CommunityToolkit.Mvvm.Messaging;
using ProjectApp.Client.Maui.Messages;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ClientCreateViewModel : ObservableObject
{
    private readonly ApiClientsService _clients;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string? phone;

    [ObservableProperty]
    private string? inn;

    [ObservableProperty]
    private ClientType type = ClientType.Individual;

    [ObservableProperty]
    private bool isBusy;

    public ClientCreateViewModel(ApiClientsService clients)
    {
        _clients = clients;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            var draft = new ClientCreateDraft { Name = Name, Phone = Phone, Inn = Inn, Type = Type };
            var id = await _clients.CreateAsync(draft);
            await Application.Current!.MainPage!.DisplayAlert("Успех", "Клиент создан", "OK");
            // Broadcast pick so caller (e.g., quick sale) can auto-select
            WeakReferenceMessenger.Default.Send(new ClientPickedMessage(id, draft.Name));
            // Navigate to detail
            await Application.Current!.MainPage!.Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert("Ошибка", ex.Message, "OK");
        }
        finally { IsBusy = false; }
    }
}
