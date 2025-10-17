namespace ProjectApp.Api.Models;

/// <summary>
/// Бонусы менеджеров
/// </summary>
public class ManagerBonus
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    
    // Расчетные данные
    public decimal TotalSales { get; set; }           // Общий оборот
    public decimal OwnClientsSales { get; set; }      // Оборот по своим клиентам
    public decimal BonusAmount { get; set; }          // Сумма бонуса
    public decimal BonusPercent { get; set; }         // Процент бонуса
    
    // Статистика
    public int SalesCount { get; set; }
    public int OwnClientsCount { get; set; }
    
    // Статус
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
}

/// <summary>
/// Настройки системы бонусов
/// </summary>
public class BonusSettings
{
    public decimal BasePercent { get; set; } = 3.0m;          // Базовый % от оборота
    public decimal OwnClientsPercent { get; set; } = 5.0m;    // % от своих клиентов
    public decimal MinimumSales { get; set; } = 10000000m;    // Минимальный оборот для бонуса
    public bool Enabled { get; set; } = true;
}
