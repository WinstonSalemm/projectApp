namespace ProjectApp.Api.Models;

/// <summary>
/// Движение денежных средств между кассами
/// </summary>
public class CashTransaction
{
    public int Id { get; set; }
    
    /// <summary>
    /// Тип транзакции
    /// </summary>
    public CashTransactionType Type { get; set; }
    
    /// <summary>
    /// Касса-источник (откуда деньги)
    /// </summary>
    public int? FromCashboxId { get; set; }
    public Cashbox? FromCashbox { get; set; }
    
    /// <summary>
    /// Касса-назначение (куда деньги)
    /// </summary>
    public int? ToCashboxId { get; set; }
    public Cashbox? ToCashbox { get; set; }
    
    /// <summary>
    /// Сумма
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Валюта
    /// </summary>
    public string Currency { get; set; } = "UZS";
    
    /// <summary>
    /// Категория транзакции
    /// </summary>
    public string? Category { get; set; }
    
    /// <summary>
    /// Описание/назначение платежа
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Связь с продажей (если это доход от продажи)
    /// </summary>
    public int? LinkedSaleId { get; set; }
    
    /// <summary>
    /// Связь с закупкой (если это расход на закупку)
    /// </summary>
    public int? LinkedPurchaseId { get; set; }
    
    /// <summary>
    /// Связь с операционным расходом
    /// </summary>
    public int? LinkedExpenseId { get; set; }
    
    /// <summary>
    /// Кто создал транзакцию
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Статус транзакции
    /// </summary>
    public TransactionStatus Status { get; set; } = TransactionStatus.Completed;
}

/// <summary>
/// Тип транзакции
/// </summary>
public enum CashTransactionType
{
    Income = 0,      // Приход (в кассу)
    Expense = 1,     // Расход (из кассы)
    Transfer = 2,    // Перемещение между кассами
    SalePayment = 3, // Оплата от клиента
    Withdrawal = 4   // Инкассация
}

/// <summary>
/// Статус транзакции
/// </summary>
public enum TransactionStatus
{
    Pending = 0,     // Ожидает
    Completed = 1,   // Выполнена
    Cancelled = 2    // Отменена
}
