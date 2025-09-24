using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.Views;

namespace ProjectApp.Client.Maui;

public partial class App : Application
{
    public App(QuickSalePage quickSalePage)
    {
        InitializeComponent();
        MainPage = new NavigationPage(quickSalePage);
    }
}
