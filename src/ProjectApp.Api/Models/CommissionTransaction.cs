namespace ProjectApp.Api.Models;

/// <summary>
/// Тип транзакции комиссии
/// </summary>
public enum CommissionTransactionType
{
    /// <summary>
    /// Начисление комиссии за продажу
    /// </summary>
    Accrual = 0,
    
    /// <summary>
    /// Начисление комиссии за договор
    /// </summary>
    ContractAccrual = 1,
    
    /// <summary>
    /// Выплата наличными
    /// </summary>
    PaymentCash = 2,
    
    /// <summary>
    /// Выплата на карту
    /// </summary>
    PaymentCard = 3,
    
    /// <summary>
    /// Выплата товаром
    /// </summary>
    PaymentProduct = 4,
    
    /// <summary>
    /// Корректировка (ручная)
    /// </summary>
    Adjustment = 5
}

/// <summary>
/// Транзакция комиссии партнера
/// </summary>
public class CommissionTransaction
{
    public int Id { get; set; }
    
    /// <summary>
    /// ID клиента-партнера
    /// </summary>
    public int CommissionAgentId { get; set; }
    
    /// <summary>
    /// Тип транзакции
    /// </summary>
    public CommissionTransactionType Type { get; set; }
    
    /// <summary>
    /// Сумма транзакции
    /// + для начислений
    /// - для выплат
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Баланс после транзакции
    /// </summary>
    public decimal BalanceAfter { get; set; }
    
    /// <summary>
    /// ID связанной продажи (если Type = Accrual или PaymentProduct)
    /// </summary>
    public int? RelatedSaleId { get; set; }
    
    /// <summary>
    /// ID связанного договора (если Type = ContractAccrual)
    /// </summary>
    public int? RelatedContractId { get; set; }
    
    /// <summary>
    /// Дата транзакции
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Кто создал транзакцию
    /// </summary>
    public string? CreatedBy { get; set; }
    
    /// <summary>
    /// Примечания
    /// </summary>
    public string? Notes { get; set; }
}
