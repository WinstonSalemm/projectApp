using ProjectApp.Client.Maui.Models;
using System.IO;

namespace ProjectApp.Client.Maui.Services;

public class SalesResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? SaleId { get; set; }
    public static SalesResult Ok(int? saleId = null) => new SalesResult { Success = true, SaleId = saleId };
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
    Task<bool> UploadSalePhotoAsync(int saleId, Stream photoStream, string fileName, CancellationToken ct = default);
}

public interface ISuppliesService
{
    Task<bool> CreateSupplyAsync(SupplyDraft draft, CancellationToken ct = default);
    Task<bool> TransferToIm40Async(string code, List<SupplyTransferItem> items, CancellationToken ct = default);
}

public interface IReturnsService
{
    Task<bool> CreateReturnAsync(ReturnDraft draft, CancellationToken ct = default);
    Task<bool> CancelBySaleAsync(int saleId, CancellationToken ct = default);
}

public interface IStocksService
{
    Task<IEnumerable<StockViewModel>> GetStocksAsync(string? query = null, string? category = null, CancellationToken ct = default);
    Task<IEnumerable<BatchStockViewModel>> GetBatchesAsync(string? query = null, string? category = null, CancellationToken ct = default);
}

// Reservations (client API)
public class ReservationCreateItemDraft
{
    public int ProductId { get; set; }
    public ProjectApp.Client.Maui.Models.StockRegister Register { get; set; } = ProjectApp.Client.Maui.Models.StockRegister.IM40; // default
    public decimal Qty { get; set; }
}

public class ReservationCreateDraft
{
    public int? ClientId { get; set; }
    public bool Paid { get; set; }
    public string? Note { get; set; }
    public List<ReservationCreateItemDraft> Items { get; set; } = new();
}

public interface IReservationsService
{
    Task<int?> CreateReservationAsync(ReservationCreateDraft draft, bool waitForPhoto, string source, CancellationToken ct = default);
    Task<bool> UploadReservationPhotoAsync(int reservationId, Stream photoStream, string fileName, CancellationToken ct = default);
}

// Draft models for submitting sales from the client
public class SaleDraft
{
    public int? ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public PaymentType PaymentType { get; set; } = PaymentType.CashWithReceipt;
    public List<SaleDraftItem> Items { get; set; } = new();
    public List<string>? ReservationNotes { get; set; }
    // Android: request API to hold text notify; client will send photo+caption
    public bool? NotifyHold { get; set; }
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

public class SupplyDraftItem : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

    public int ProductId { get; set; }

    private decimal _qty;
    public decimal Qty
    {
        get => _qty;
        set { if (_qty != value) { _qty = value; OnPropertyChanged(nameof(Qty)); OnPropertyChanged(nameof(Total)); } }
    }

    private decimal _unitCost;
    public decimal UnitCost
    {
        get => _unitCost;
        set { if (_unitCost != value) { _unitCost = value; OnPropertyChanged(nameof(UnitCost)); OnPropertyChanged(nameof(Total)); } }
    }

    public string Code { get; set; } = string.Empty;
    public string? Note { get; set; }
    // Optional, for UI display only (not used by API)
    public string? Sku { get; set; }
    public string? Name { get; set; }
    public decimal Total => Qty * UnitCost;
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

    public async Task<IEnumerable<ProductModel>> SearchAsync(string? query, string? category = null, CancellationToken ct = default)
    {
        if (_settings.UseApi)
        {
            return await _api.SearchAsync(query, category, ct);
        }
        return await _mock.SearchAsync(query, category, ct);
    }

    public async Task<IEnumerable<string>> GetCategoriesAsync(CancellationToken ct = default)
    {
        if (_settings.UseApi)
        {
            return await _api.GetCategoriesAsync(ct);
        }
        return await _mock.GetCategoriesAsync(ct);
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
        => _settings.UseApi ? _api.SubmitSaleAsync(draft, ct) : _mock.SubmitSaleAsync(draft, ct);

    public Task<bool> UploadSalePhotoAsync(int saleId, Stream photoStream, string fileName, CancellationToken ct = default)
        => _api.UploadSalePhotoAsync(saleId, photoStream, fileName, ct);
}

public interface IProductsService
{
    Task<bool> CreateCategoryAsync(string name, CancellationToken ct = default);
    Task<int?> CreateProductAsync(ProductCreateDraft draft, CancellationToken ct = default);
}

// ----- Products (create product and create category) -----
public class ProductCreateDraft
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "шт";
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
}

// ===== Finance client API (JSON-first for lightweight UI) =====
public interface IFinanceService
{
    Task<string> GetSummaryJsonAsync(DateTime? from, DateTime? to, string? bucketBy = null, string? groupBy = null, CancellationToken ct = default);
    Task<string> GetKpiJsonAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<string> GetCashFlowJsonAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<string> GetAbcJsonAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<string> GetXyzJsonAsync(DateTime? from, DateTime? to, string bucket = "month", CancellationToken ct = default);
    Task<string> GetTrendsJsonAsync(DateTime? from, DateTime? to, string metric = "revenue", string interval = "month", CancellationToken ct = default);
    Task<string> GetTaxesBreakdownJsonAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<string> GetClientsJsonAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<string> GetAlertsPreviewJsonAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
}

