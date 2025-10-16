namespace ProjectApp.Api.Models;

public enum ExpenseCategory
{
    Rent,           // Аренда
    Salary,         // Зарплата
    Utilities,      // Коммунальные
    Transport,      // Транспорт
    Marketing,      // Маркетинг
    Maintenance,    // Обслуживание
    Communication,  // Связь
    Other           // Прочее
}

public class Expense
{
    public int Id { get; set; }
    public ExpenseCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
