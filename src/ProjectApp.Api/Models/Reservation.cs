namespace ProjectApp.Api.Models;

public enum ReservationStatus
{
    Active = 0,
    Released = 1,
    Expired = 2,
    Fulfilled = 3
}

public class Reservation
{
    public int Id { get; set; }
    public int? ClientId { get; set; }
    public int? SaleId { get; set; }
    public int? ContractId { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool Paid { get; set; }
    public DateTime ReservedUntil { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Active;
    public string? Note { get; set; }

    // Optional manager photo captured at reservation creation
    public string? PhotoPath { get; set; }
    public string? PhotoMime { get; set; }
    public long? PhotoSize { get; set; }
    public DateTime? PhotoCreatedAt { get; set; }

    // Items snapshot at reservation time (each row stores register, qty, unit price)
    public List<ReservationItem> Items { get; set; } = new();
}

public class ReservationItem
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public int ProductId { get; set; }
    public StockRegister Register { get; set; }
    public decimal Qty { get; set; }

    // Snapshot fields to keep exact data used at reservation time
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
}

public class ReservationLog
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public string Action { get; set; } = string.Empty; // Created, Extended, Released, Expired, Fulfilled
    public string UserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Details { get; set; }
}
