namespace ProjectApp.Api.Models;

public enum DebtStatus
{
    Open = 0,      // Открыт (активный долг)
    Paid = 1,      // Оплачен полностью
    Overdue = 2,   // Просрочен (дата прошла)
    Canceled = 3   // Отменен
}

public class Debt
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public int SaleId { get; set; }
    
    /// <summary>
    /// Текущая сумма долга (уменьшается при оплате)
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Изначальная сумма долга (не меняется)
    /// </summary>
    public decimal OriginalAmount { get; set; }
    
    /// <summary>
    /// Срок оплаты
    /// </summary>
    public DateTime DueDate { get; set; }
    
    /// <summary>
    /// Статус долга
    /// </summary>
    public DebtStatus Status { get; set; }
    
    /// <summary>
    /// Товары в составе долга (можно редактировать)
    /// </summary>
    public List<DebtItem> Items { get; set; } = new();
    
    /// <summary>
    /// Примечания
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Дата создания
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Кто создал
    /// </summary>
    public string? CreatedBy { get; set; }
}
