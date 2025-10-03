using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.Services;

public class SalesResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public static SalesResult Ok() => new SalesResult { Success = true };
    public static SalesResult Fail(string? message) => new SalesResult { Success = false, ErrorMessage = message };
}

public class ContractListItem
{
    public int Id { get; set; }
    public string OrgName { get; set; } = string.Empty;
    public string? Inn { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = "Signed";
    public DateTime CreatedAt { get; set; }
    public string? Note { get; set; }
}

public class ContractItemDraft
{
    public int? ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "шт";
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
}

public class ContractCreateDraft
{
    public string OrgName { get; set; } = string.Empty;
    public string? Inn { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = "Signed"; // Signed | Paid | Closed
    public string? Note { get; set; }
    public List<ContractItemDraft> Items { get; set; } = new();
}

public class ContractDetail
{
    public int Id { get; set; }
    public string OrgName { get; set; } = string.Empty;
    public string? Inn { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = "Signed";
    public DateTime CreatedAt { get; set; }
    public string? Note { get; set; }
    public List<ContractItemDraft> Items { get; set; } = new();
}

public interface IContractsService
{
    Task<IEnumerable<ContractListItem>> ListAsync(string? status = null, CancellationToken ct = default);
    Task<bool> CreateAsync(ContractCreateDraft draft, CancellationToken ct = default);
    Task<bool> UpdateStatusAsync(int id, string status, CancellationToken ct = default);
    Task<ContractDetail?> GetAsync(int id, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, ContractCreateDraft draft, CancellationToken ct = default);
}

public interface ICatalogService
{
    Task<IEnumerable<ProductModel>> SearchAsync(string? query, string? category = null, CancellationToken ct = default);
    Task<IEnumerable<string>> GetCategoriesAsync(CancellationToken ct = default);
}

public interface ISalesService
{
    Task<SalesResult> SubmitSaleAsync(SaleDraft draft, CancellationToken ct = default);
}

public interface ISuppliesService
{
    Task<bool> CreateSupplyAsync(SupplyDraft draft, CancellationToken ct = default);
    Task<bool> TransferToIm40Async(string code, List<SupplyTransferItem> items, CancellationToken ct = default);
}

public interface IReturnsService
{
    Task<bool> CreateReturnAsync(ReturnDraft draft, CancellationToken ct = default);
}

public interface IStocksService
{
    Task<IEnumerable<StockViewModel>> GetStocksAsync(string? query = null, string? category = null, CancellationToken ct = default);
    Task<IEnumerable<BatchStockViewModel>> GetBatchesAsync(string? query = null, string? category = null, CancellationToken ct = default);
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
    public decimal UnitPrice { get; set; }
}

// Supplies
public class SupplyDraft
{
    public List<SupplyDraftItem> Items { get; set; } = new();
}

public class SupplyDraftItem
{
    public int ProductId { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public class SupplyTransferItem
{
    public int ProductId { get; set; }
    public decimal Qty { get; set; }
}

// Returns
public class ReturnDraft
{
    public int RefSaleId { get; set; }
    public int? ClientId { get; set; }
    public string? Reason { get; set; }
    // if null or empty -> full return
    public List<ReturnDraftItem>? Items { get; set; }
}

public class ReturnDraftItem
{
    public int SaleItemId { get; set; }
    public decimal Qty { get; set; }
}

public class StockViewModel
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Nd40Qty { get; set; }
    public decimal Im40Qty { get; set; }
    public decimal TotalQty { get; set; }
}

public class BatchStockViewModel
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Register { get; set; } = string.Empty; // ND40 / IM40
    public string? Code { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Note { get; set; }
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

    public Task<IEnumerable<ProductModel>> SearchAsync(string? query, string? category = null, CancellationToken ct = default)
    {
        return _settings.UseApi ? _api.SearchAsync(query, category, ct) : _mock.SearchAsync(query, category, ct);
    }

    public Task<IEnumerable<string>> GetCategoriesAsync(CancellationToken ct = default)
    {
        return _settings.UseApi ? _api.GetCategoriesAsync(ct) : _mock.GetCategoriesAsync(ct);
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

    public Task<SalesResult> SubmitSaleAsync(SaleDraft draft, CancellationToken ct = default)
    {
        return _settings.UseApi ? _api.SubmitSaleAsync(draft, ct) : _mock.SubmitSaleAsync(draft, ct);
    }
}
