using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class ReturnSourceSelectorPage : ContentPage
{
    public ReturnSourceSelectorPage(ReturnSourceSelectorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
