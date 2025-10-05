using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.Messages;
using ProjectApp.Client.Maui.Models;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class ClientEditViewModel : ObservableObject
{
    private readonly ApiClientsService _clients;

    [ObservableProperty]
    private int clientId;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string? phone;

    [ObservableProperty]
    private string? inn;

    [ObservableProperty]
    private ClientType? type;

    [ObservableProperty]
    private bool isBusy;

    public ClientEditViewModel(ApiClientsService clients)
    {
        _clients = clients;
    }

    public async Task LoadAsync(int id)
    {
        ClientId = id;
        var dto = await _clients.GetAsync(id);
        if (dto != null)
        {
            Name = dto.Name;
            Phone = dto.Phone;
            Inn = dto.Inn;
            Type = dto.Type;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            var draft = new ClientUpdateDraft { Name = Name, Phone = Phone, Inn = Inn, Type = Type };
            var ok = await _clients.UpdateAsync(ClientId, draft);
            if (!ok) { await Application.Current!.MainPage!.DisplayAlert("Ошибка", "Не удалось сохранить", "OK"); return; }
            WeakReferenceMessenger.Default.Send(new ClientUpdatedMessage(ClientId, Name, Phone, Inn, Type ?? ClientType.Individual));
            await Application.Current!.MainPage!.DisplayAlert("Сохранено", "Изменения сохранены", "OK");
            await Application.Current!.MainPage!.Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert("Ошибка", ex.Message, "OK");
        }
        finally { IsBusy = false; }
    }
}
