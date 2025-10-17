namespace ProjectApp.Api.Models;

/// <summary>
/// Запись об отгрузке товара по договору
/// </summary>
public class ContractDelivery
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public int ContractItemId { get; set; }  // Какая позиция договора
    public int ProductId { get; set; }
    public decimal Qty { get; set; }
    public DateTime DeliveredAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? Note { get; set; }
    
    // Связь с партиями - откуда взяли товар
    public List<ContractDeliveryBatch> Batches { get; set; } = new();
}

/// <summary>
/// Связь отгрузки с конкретными партиями товара
/// </summary>
public class ContractDeliveryBatch
{
    public int Id { get; set; }
    public int ContractDeliveryId { get; set; }
    public int BatchId { get; set; }              // Партия товара
    public StockRegister RegisterAtDelivery { get; set; }  // В каком регистре была партия при отгрузке
    public decimal Qty { get; set; }              // Сколько взяли из этой партии
    public decimal UnitCost { get; set; }         // Себестоимость на момент отгрузки
}
