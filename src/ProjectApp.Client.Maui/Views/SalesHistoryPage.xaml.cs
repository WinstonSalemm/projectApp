using Microsoft.Maui.Controls;
using ProjectApp.Client.Maui.ViewModels;

namespace ProjectApp.Client.Maui.Views;

public partial class SalesHistoryPage : ContentPage
{
    public SalesHistoryPage(SalesHistoryViewModel vm)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[SalesHistoryPage] Constructor START");
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("[SalesHistoryPage] InitializeComponent done");
            BindingContext = vm;
            System.Diagnostics.Debug.WriteLine("[SalesHistoryPage] BindingContext set");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SalesHistoryPage] CRASH in constructor: {ex}");
            throw;
        }
    }
}

