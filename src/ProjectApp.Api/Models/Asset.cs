namespace ProjectApp.Api.Models;

public enum AssetCategory
{
    Equipment,  // Оборудование
    Vehicle,    // Транспорт
    Furniture,  // Мебель
    RealEstate, // Недвижимость
    Other       // Прочее
}

public class Asset
{
    public int Id { get; set; }
    public AssetCategory Category { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PurchasePrice { get; set; }
    public DateTime PurchaseDate { get; set; }
    public int UsefulLifeYears { get; set; } // Срок полезного использования
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Расчетные поля
    public decimal AnnualDepreciation => UsefulLifeYears > 0 ? PurchasePrice / UsefulLifeYears : 0;
    public decimal CurrentValue
    {
        get
        {
            var yearsPassed = (DateTime.UtcNow - PurchaseDate).Days / 365.0;
            var depreciation = AnnualDepreciation * (decimal)yearsPassed;
            return Math.Max(0, PurchasePrice - depreciation);
        }
    }
}
