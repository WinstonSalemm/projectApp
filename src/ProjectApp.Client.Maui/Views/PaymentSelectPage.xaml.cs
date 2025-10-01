using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectApp.Client.Maui.Views;

public partial class PaymentSelectPage : ContentPage
{
    public PaymentSelectPage(ProjectApp.Client.Maui.ViewModels.PaymentSelectViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
