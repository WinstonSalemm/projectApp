using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class ClientSelectPage : ContentPage
{
    public event EventHandler<(int? Id, string Name)>? ClientSelected;
    
    private readonly ClientSelectViewModel _vm;

    public ClientSelectPage(ClientSelectViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.SearchCommand.Execute(null);
    }

    private void OnAllTypesClicked(object? sender, EventArgs e)
    {
        _vm.SelectedType = null;
        _vm.SearchCommand.Execute(null);
    }

    private void OnLegalClicked(object? sender, EventArgs e)
    {
        _vm.SelectedType = "Legal";
        _vm.SearchCommand.Execute(null);
    }

    private void OnIndividualClicked(object? sender, EventArgs e)
    {
        _vm.SelectedType = "Individual";
        _vm.SearchCommand.Execute(null);
    }

    private void OnClientSelected(object? sender, EventArgs e)
    {
        if (sender is Border border && border.GestureRecognizers[0] is TapGestureRecognizer tap && tap.CommandParameter is ClientSelectViewModel.ClientRow client)
        {
            ClientSelected?.Invoke(this, (client.Id, client.Name));
        }
    }

    private void OnSkipClient(object? sender, EventArgs e)
    {
        ClientSelected?.Invoke(this, (null, string.Empty));
    }
}
