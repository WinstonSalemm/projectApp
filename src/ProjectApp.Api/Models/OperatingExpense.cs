namespace ProjectApp.Api.Models;

/// <summary>
/// Операционные расходы бизнеса
/// </summary>
public class OperatingExpense
{
    public int Id { get; set; }
    
    /// <summary>
    /// Тип расхода
    /// </summary>
    public ExpenseType Type { get; set; }
    
    /// <summary>
    /// Сумма расхода
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Валюта
    /// </summary>
    public string Currency { get; set; } = "UZS";
    
    /// <summary>
    /// Дата расхода
    /// </summary>
    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Описание расхода
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Категория расхода (для группировки)
    /// </summary>
    public string? Category { get; set; }
    
    /// <summary>
    /// Является ли регулярным расходом (ежемесячный)
    /// </summary>
    public bool IsRecurring { get; set; } = false;
    
    /// <summary>
    /// Период повторения для регулярных расходов
    /// </summary>
    public RecurringPeriod? RecurringPeriod { get; set; }
    
    /// <summary>
    /// Касса, из которой был произведен расход
    /// </summary>
    public int? CashboxId { get; set; }
    public Cashbox? Cashbox { get; set; }
    
    /// <summary>
    /// Получатель платежа
    /// </summary>
    public string? Recipient { get; set; }
    
    /// <summary>
    /// Кто создал запись
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Статус оплаты
    /// </summary>
    public ExpensePaymentStatus PaymentStatus { get; set; } = ExpensePaymentStatus.Paid;
    
    /// <summary>
    /// Дата оплаты
    /// </summary>
    public DateTime? PaidAt { get; set; }
}

/// <summary>
/// Тип операционного расхода
/// </summary>
public enum ExpenseType
{
    Salary = 0,        // Зарплата
    Rent = 1,          // Аренда
    Utilities = 2,     // Коммунальные услуги
    Transport = 3,     // Транспорт и доставка
    Customs = 4,       // Таможенные сборы
    Logistics = 5,     // Логистика
    Marketing = 6,     // Маркетинг и реклама
    Taxes = 7,         // Налоги и сборы
    Insurance = 8,     // Страхование
    Maintenance = 9,   // Обслуживание и ремонт
    Office = 10,       // Офисные расходы
    Communication = 11,// Связь (интернет, телефон)
    Banking = 12,      // Банковские комиссии
    Legal = 13,        // Юридические услуги
    Other = 99         // Прочие расходы
}

/// <summary>
/// Период повторения для регулярных расходов
/// </summary>
public enum RecurringPeriod
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
    Quarterly = 3,
    Yearly = 4
}

/// <summary>
/// Статус оплаты расхода
/// </summary>
public enum ExpensePaymentStatus
{
    Pending = 0,  // Ожидает оплаты
    Paid = 1,     // Оплачено
    Overdue = 2   // Просрочено
}
