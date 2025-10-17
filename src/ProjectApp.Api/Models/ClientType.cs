namespace ProjectApp.Api.Models;

public enum ClientType
{
    Individual = 1,        // Физлицо
    Company = 2,           // Юрлицо
    Retail = 3,            // Розница (малые объемы)
    Wholesale = 4,         // Опт (средние объемы)
    LargeWholesale = 5     // Крупный опт (большие объемы)
}
