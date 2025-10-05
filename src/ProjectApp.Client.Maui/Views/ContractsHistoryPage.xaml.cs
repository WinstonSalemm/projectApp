using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class ContractsHistoryPage : ContentPage
{
    public ContractsHistoryPage(ContractsHistoryViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
