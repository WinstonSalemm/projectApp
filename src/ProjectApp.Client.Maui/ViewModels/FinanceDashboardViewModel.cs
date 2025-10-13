using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectApp.Client.Maui.Services;
using System.Text.Json;

namespace ProjectApp.Client.Maui.ViewModels;

public partial class FinanceDashboardViewModel : ObservableObject
{
    private readonly IFinanceService _finance;

    [ObservableProperty] private DateTime? from = DateTime.UtcNow.AddDays(-30);
    [ObservableProperty] private DateTime? to = DateTime.UtcNow;

    [ObservableProperty] private string summaryJson = string.Empty;
    [ObservableProperty] private string kpiJson = string.Empty;
    [ObservableProperty] private string cashflowJson = string.Empty;
    [ObservableProperty] private string abcJson = string.Empty;
    [ObservableProperty] private string xyzJson = string.Empty;
    [ObservableProperty] private string trendsJson = string.Empty;
    [ObservableProperty] private string taxesJson = string.Empty;
    [ObservableProperty] private string clientsJson = string.Empty;
    [ObservableProperty] private string alertsJson = string.Empty;

    [ObservableProperty] private bool isBusy;

    // Typed metrics (from summary)
    [ObservableProperty] private decimal revenue;
    [ObservableProperty] private decimal cogs;
    [ObservableProperty] private decimal grossProfit;
    [ObservableProperty] private decimal netProfit;
    [ObservableProperty] private decimal marginPercent;
    [ObservableProperty] private decimal expenses;
    [ObservableProperty] private decimal taxesPaid;
    [ObservableProperty] private int salesCount;
    [ObservableProperty] private int uniqueClients;
    [ObservableProperty] private decimal averageInventory;

    // Deltas vs previous period
    [ObservableProperty] private decimal revenueDeltaPercent;
    [ObservableProperty] private decimal netProfitDeltaPercent;

    public FinanceDashboardViewModel(IFinanceService finance)
    {
        _finance = finance;
    }

    [RelayCommand]
    public async Task RefreshAllAsync()
    {
        try
        {
            IsBusy = true;
            var f = From; var t = To;
            var tasks = new List<Task>();
            tasks.Add(Task.Run(async () =>
            {
                var json = await _finance.GetSummaryJsonAsync(f, t);
                SummaryJson = json;
                TryParseSummary(json);
                // previous period
                if (f.HasValue && t.HasValue)
                {
                    var span = t.Value - f.Value;
                    var pf = f.Value - span;
                    var pt = f.Value;
                    var prevJson = await _finance.GetSummaryJsonAsync(pf, pt);
                    ComputeDeltas(json, prevJson);
                }
            }));
            tasks.Add(Task.Run(async () => KpiJson = await _finance.GetKpiJsonAsync(f, t)));
            tasks.Add(Task.Run(async () => CashflowJson = await _finance.GetCashFlowJsonAsync(f, t)));
            tasks.Add(Task.Run(async () => AbcJson = await _finance.GetAbcJsonAsync(f, t)));
            tasks.Add(Task.Run(async () => XyzJson = await _finance.GetXyzJsonAsync(f, t, "month")));
            tasks.Add(Task.Run(async () => TrendsJson = await _finance.GetTrendsJsonAsync(f, t)));
            tasks.Add(Task.Run(async () => TaxesJson = await _finance.GetTaxesBreakdownJsonAsync(f, t)));
            tasks.Add(Task.Run(async () => ClientsJson = await _finance.GetClientsJsonAsync(f, t)));
            tasks.Add(Task.Run(async () => AlertsJson = await _finance.GetAlertsPreviewJsonAsync(f, t)));
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            SummaryJson = ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task SetPresetDaysAsync(int days)
    {
        To = DateTime.UtcNow;
        From = DateTime.UtcNow.AddDays(-Math.Abs(days));
        await RefreshAllAsync();
    }

    private void TryParseSummary(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return;
            if (r.TryGetProperty("revenue", out var v)) Revenue = v.GetDecimal();
            if (r.TryGetProperty("cogs", out v)) Cogs = v.GetDecimal();
            if (r.TryGetProperty("grossProfit", out v)) GrossProfit = v.GetDecimal();
            if (r.TryGetProperty("netProfit", out v)) NetProfit = v.GetDecimal();
            if (r.TryGetProperty("marginPercent", out v)) MarginPercent = v.GetDecimal();
            if (r.TryGetProperty("expenses", out v)) Expenses = v.GetDecimal();
            if (r.TryGetProperty("taxesPaid", out v)) TaxesPaid = v.GetDecimal();
            if (r.TryGetProperty("salesCount", out v)) SalesCount = v.GetInt32();
            if (r.TryGetProperty("uniqueClients", out v)) UniqueClients = v.GetInt32();
            if (r.TryGetProperty("averageInventory", out v)) AverageInventory = v.GetDecimal();
        }
        catch { }
    }

    private void ComputeDeltas(string currentJson, string prevJson)
    {
        try
        {
            decimal curRev = 0, prevRev = 0, curNet = 0, prevNet = 0;
            using (var cdoc = JsonDocument.Parse(currentJson))
            {
                var r = cdoc.RootElement;
                if (r.TryGetProperty("revenue", out var v)) curRev = SafeDecimal(v);
                if (r.TryGetProperty("netProfit", out v)) curNet = SafeDecimal(v);
            }
            using (var pdoc = JsonDocument.Parse(prevJson))
            {
                var r = pdoc.RootElement;
                if (r.TryGetProperty("revenue", out var v)) prevRev = SafeDecimal(v);
                if (r.TryGetProperty("netProfit", out v)) prevNet = SafeDecimal(v);
            }
            RevenueDeltaPercent = ComputePct(prevRev, curRev);
            NetProfitDeltaPercent = ComputePct(prevNet, curNet);
        }
        catch { }
    }

    private static decimal SafeDecimal(JsonElement e)
        => e.ValueKind == JsonValueKind.Number ? e.GetDecimal() : 0m;

    private static decimal ComputePct(decimal prev, decimal cur)
        => prev == 0m ? (cur == 0m ? 0m : 100m) : (cur - prev) / prev * 100m;
}

