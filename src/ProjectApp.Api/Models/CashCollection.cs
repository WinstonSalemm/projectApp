namespace ProjectApp.Api.Models;

/// <summary>
/// Инкассация - сдача накопленных серых денег
/// </summary>
public class CashCollection
{
    public int Id { get; set; }
    
    /// <summary>
    /// Дата инкассации
    /// </summary>
    public DateTime CollectionDate { get; set; }
    
    /// <summary>
    /// Накоплено с последней инкассации (нал без чека + click без чека)
    /// </summary>
    public decimal AccumulatedAmount { get; set; }
    
    /// <summary>
    /// Сумма сданная при инкассации
    /// </summary>
    public decimal CollectedAmount { get; set; }
    
    /// <summary>
    /// Остаток на предприятии (Cash Flow)
    /// = AccumulatedAmount - CollectedAmount
    /// </summary>
    public decimal RemainingAmount { get; set; }
    
    /// <summary>
    /// Примечание (дивиденды учредителей, резерв и т.д.)
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Кто провел инкассацию
    /// </summary>
    public string? CreatedBy { get; set; }
    
    public DateTime CreatedAt { get; set; }
}
