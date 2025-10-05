using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ProjectApp.Client.Maui.Services;
using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class SaleStartViewModel : ObservableObject
{
    private readonly ICatalogService _catalog;

    public ObservableCollection<string> Categories { get; } = new();

    [ObservableProperty]
    private string? selectedCategory;

    [ObservableProperty]
    private PaymentType selectedPaymentType = PaymentType.CashWithReceipt;

    public IReadOnlyList<PaymentType> PaymentTypes { get; } = new[]
    {
        PaymentType.CashWithReceipt,
        PaymentType.CashNoReceipt,
        PaymentType.CardWithReceipt,
        PaymentType.ClickWithReceipt,
        PaymentType.ClickNoReceipt,
        PaymentType.Click, // legacy
        PaymentType.Site,
        PaymentType.Return,
        PaymentType.Reservation,
        PaymentType.Payme,
    };

    public SaleStartViewModel(ICatalogService catalog)
    {
        _catalog = catalog;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var list = await _catalog.GetCategoriesAsync();
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                Categories.Clear();
                foreach (var c in list)
                    Categories.Add(c);
            });
        }
        catch { }
    }
}
