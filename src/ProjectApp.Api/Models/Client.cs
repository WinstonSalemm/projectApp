namespace ProjectApp.Api.Models;

public class Client
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Inn { get; set; }
    public ClientType Type { get; set; } = ClientType.Individual;
    public string? OwnerUserName { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Статистика для автоматической классификации
    public decimal TotalPurchases { get; set; }  // Общая сумма покупок
    public int PurchasesCount { get; set; }      // Количество покупок
    public DateTime? LastPurchaseDate { get; set; }
    public DateTime? TypeAssignedAt { get; set; } // Когда был присвоен тип
    
    // ===== ПАРТНЕРСКАЯ ПРОГРАММА =====
    
    /// <summary>
    /// Является ли клиент партнером (комиссионным агентом)
    /// </summary>
    public bool IsCommissionAgent { get; set; }
    
    /// <summary>
    /// Текущий баланс комиссии (сколько мы должны партнеру)
    /// + Начислено за продажи/договоры
    /// - Выплачено деньгами/товаром
    /// </summary>
    public decimal CommissionBalance { get; set; }
    
    /// <summary>
    /// Дата когда стал партнером
    /// </summary>
    public DateTime? CommissionAgentSince { get; set; }
    
    /// <summary>
    /// Примечания по партнерству
    /// </summary>
    public string? CommissionNotes { get; set; }
}
