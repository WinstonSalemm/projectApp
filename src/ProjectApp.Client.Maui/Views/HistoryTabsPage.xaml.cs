using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;

namespace ProjectApp.Client.Maui.Views;

public partial class HistoryTabsPage : TabbedPage
{
    public HistoryTabsPage(IServiceProvider services)
    {
        InitializeComponent();

        var sales = services.GetRequiredService<SalesHistoryPage>();
        var returns = services.GetRequiredService<ReturnsHistoryPage>();
        var supplies = services.GetRequiredService<SuppliesHistoryPage>();
        var contracts = services.GetRequiredService<ContractsHistoryPage>();

        Children.Add(sales);
        Children.Add(returns);
        Children.Add(supplies);
        Children.Add(contracts);
    }
}

