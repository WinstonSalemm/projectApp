using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class DebtDetailPage : ContentPage
{
    private readonly DebtDetailViewModel _vm;

    public DebtDetailPage(DebtDetailViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.DebtId > 0)
        {
            await _vm.LoadAsync(_vm.DebtId);
        }
    }
}
