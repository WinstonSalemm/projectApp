using ProjectApp.Api.Models;

namespace ProjectApp.Api.Dtos;

public class ReservationCreateItemDto
{
    public int ProductId { get; set; }
    public StockRegister Register { get; set; }
    public decimal Qty { get; set; }
}

public class ReservationCreateDto
{
    public int? ClientId { get; set; }
    public bool Paid { get; set; }
    public string? Note { get; set; }
    public bool? WaitForPhoto { get; set; } // Android path will set true to defer TG send until photo arrives
    public string? Source { get; set; } // e.g., Windows/Android
    public List<ReservationCreateItemDto> Items { get; set; } = new();
}

public class ReservationViewDto
{
    public int Id { get; set; }
    public int? ClientId { get; set; }
    public bool Paid { get; set; }
    public DateTime ReservedUntil { get; set; }
    public ReservationStatus Status { get; set; }
    public List<ReservationItemViewDto> Items { get; set; } = new();
}

public class ReservationItemViewDto
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public StockRegister Register { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
}

public class ReservationAlertDto
{
    public int Id { get; set; }
    public string? ClientName { get; set; }
    public string? ClientPhone { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReservedUntil { get; set; }
    public bool Paid { get; set; }
    public string Status { get; set; } = "Active";
    public int? SaleId { get; set; }
}

