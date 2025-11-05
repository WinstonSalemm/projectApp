using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class ReservationDetailsPage : ContentPage
{
    public ReservationDetailsPage(ReservationDetailsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
