namespace ProjectApp.Api.Models;

/// <summary>
/// Связь брони с конкретными партиями товара (для возврата при отмене)
/// </summary>
public class ReservationItemBatch
{
    public int Id { get; set; }
    public int ReservationItemId { get; set; }
    public int BatchId { get; set; }
    public StockRegister RegisterAtReservation { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
}
