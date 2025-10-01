using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.Services;

public interface ICatalogService
{
    Task<IEnumerable<ProductModel>> SearchAsync(string? query, CancellationToken ct = default);
}

public interface ISalesService
{
    Task<bool> SubmitSaleAsync(SaleDraft draft, CancellationToken ct = default);
}

// Draft models for submitting sales from the client
public class SaleDraft
{
    public string ClientName { get; set; } = string.Empty;
    public PaymentType PaymentType { get; set; } = PaymentType.CashWithReceipt;
    public List<SaleDraftItem> Items { get; set; } = new();
    public List<string>? ReservationNotes { get; set; }
}

public class SaleDraftItem
{
    public int ProductId { get; set; }
    public double Qty { get; set; }
}

// Routed services switch between Api and Mock at runtime using AppSettings.UseApi
public class RoutedCatalogService : ICatalogService
{
    private readonly AppSettings _settings;
    private readonly ApiCatalogService _api;
    private readonly MockCatalogService _mock;

    public RoutedCatalogService(AppSettings settings, ApiCatalogService api, MockCatalogService mock)
    {
        _settings = settings;
        _api = api;
        _mock = mock;
    }

    public Task<IEnumerable<ProductModel>> SearchAsync(string? query, CancellationToken ct = default)
    {
        return _settings.UseApi ? _api.SearchAsync(query, ct) : _mock.SearchAsync(query, ct);
    }
}

public class RoutedSalesService : ISalesService
{
    private readonly AppSettings _settings;
    private readonly ApiSalesService _api;
    private readonly MockSalesService _mock;

    public RoutedSalesService(AppSettings settings, ApiSalesService api, MockSalesService mock)
    {
        _settings = settings;
        _api = api;
        _mock = mock;
    }

    public Task<bool> SubmitSaleAsync(SaleDraft draft, CancellationToken ct = default)
    {
        return _settings.UseApi ? _api.SubmitSaleAsync(draft, ct) : _mock.SubmitSaleAsync(draft, ct);
    }
}
