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
        IEnumerable<string> list;
        try { list = await _catalog.GetCategoriesAsync(); }
        catch { list = Array.Empty<string>(); }

        if (list is null || !list.Any())
        {
            // Strong local defaults so экран никогда не пустой
            list = new[]
            {
                "Огнетушители",
                "Огнетушители ПОРОШКОВЫЕ",
                "Огнетушители УГЛЕКИСЛОТНЫЕ",
                "Кронштейны",
                "Подставки",
                "Шкафы",
                "датчики",
                "рукава"
            };
        }

        await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
        {
            Categories.Clear();
            // Always provide a placeholder to continue without filtering by category
            Categories.Add("(Без категории)");
            foreach (var c in list.Distinct().OrderBy(s => s))
                Categories.Add(c);
        });
    }
}
