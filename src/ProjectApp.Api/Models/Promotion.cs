namespace ProjectApp.Api.Models;

/// <summary>
/// Акция/скидка на товары
/// </summary>
public class Promotion
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public PromotionType Type { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal? DiscountAmount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? Note { get; set; }

    public List<PromotionItem> Items { get; set; } = new();
}

/// <summary>
/// Товары в акции
/// </summary>
public class PromotionItem
{
    public int Id { get; set; }
    public int PromotionId { get; set; }
    public int ProductId { get; set; }
    public decimal? CustomDiscountPercent { get; set; }  // Индивидуальная скидка для товара
}

public enum PromotionType
{
    PercentDiscount = 1,    // Процентная скидка
    FixedDiscount = 2,      // Фиксированная скидка
    Clearance = 3,          // Распродажа
    Bundle = 4              // Комплект
}
