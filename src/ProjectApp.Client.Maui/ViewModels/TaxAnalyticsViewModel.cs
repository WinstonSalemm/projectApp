using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ProjectApp.Client.Maui.Services;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class TaxAnalyticsViewModel : ObservableObject
{
    private readonly TaxApiService _taxApi;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private decimal totalRevenue;

    [ObservableProperty]
    private decimal revenueWithoutVAT;

    [ObservableProperty]
    private decimal vatFromSales;

    [ObservableProperty]
    private decimal vatPayable;

    [ObservableProperty]
    private decimal incomeTax;

    [ObservableProperty]
    private decimal socialTax;

    [ObservableProperty]
    private decimal totalTaxes;

    [ObservableProperty]
    private decimal netProfit;

    [ObservableProperty]
    private decimal netProfitMargin;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string periodText = "Текущий месяц";

    public TaxAnalyticsViewModel(TaxApiService taxApi)
    {
        _taxApi = taxApi;
    }

    public async Task LoadMonthlyReportAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var now = DateTime.Now;
            var report = await _taxApi.GetMonthlyTaxReportAsync(now.Year, now.Month);

            if (report != null)
            {
                TotalRevenue = report.TotalRevenue;
                RevenueWithoutVAT = report.RevenueWithoutVAT;
                VatFromSales = report.VatFromSales;
                VatPayable = report.VatPayable;
                IncomeTax = report.IncomeTax;
                SocialTax = report.SocialTax + report.Inps + report.SchoolFund;
                TotalTaxes = report.TotalTaxes;
                NetProfit = report.NetProfit;
                NetProfitMargin = report.NetProfitMargin;

                PeriodText = $"{now:MMMM yyyy}";

                System.Diagnostics.Debug.WriteLine($"[TaxAnalyticsViewModel] Loaded report for {PeriodText}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TaxAnalyticsViewModel] LoadMonthlyReportAsync error: {ex}");
            ErrorMessage = "Ошибка загрузки налогового отчета";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
