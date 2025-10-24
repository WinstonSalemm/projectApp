using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ProjectApp.Api.Models;

public class CostingSession
{
    public int Id { get; set; }
    
    public int SupplyId { get; set; }
    public Supply Supply { get; set; } = null!;
    
    // Ручной ввод пользователем (все поля обязательны к редактированию в UI)
    [Precision(18, 4)]
    public decimal ExchangeRate { get; set; } // курс RUB→UZS (ввод вручную)
    
    // Проценты (к «цена сум»)
    [Precision(18, 4)]
    public decimal VatPct { get; set; } // НДС (пример: 0.22 = 22%), ввод вручную
    
    [Precision(18, 4)]
    public decimal LogisticsPct { get; set; } // логистика (0.005), ввод вручную
    
    [Precision(18, 4)]
    public decimal StoragePct { get; set; } // склад (0.002), ввод вручную
    
    [Precision(18, 4)]
    public decimal DeclarationPct { get; set; } // декл (0.01), ввод вручную
    
    [Precision(18, 4)]
    public decimal CertificationPct { get; set; } // сертиф (0.01), ввод вручную
    
    [Precision(18, 4)]
    public decimal MChsPct { get; set; } // МЧС (процент), ввод вручную
    
    [Precision(18, 4)]
    public decimal UnforeseenPct { get; set; } // непредвиденные (0.015), ввод вручную
    
    // Абсолюты (UZS), распределяются по количеству (шт)
    [Precision(18, 4)]
    public decimal CustomsFeeAbs { get; set; } // «там., сбор», ввод вручную
    
    [Precision(18, 4)]
    public decimal LoadingAbs { get; set; } // «погрузка», ввод вручную
    
    [Precision(18, 4)]
    public decimal ReturnsAbs { get; set; } // «возврат», если нужен, ввод вручную
    
    public ApportionMethod ApportionMethod { get; set; } = ApportionMethod.ByQuantity;
    
    public bool IsFinalized { get; set; } // после фикса — только чтение
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public List<CostingItemSnapshot> ItemSnapshots { get; set; } = new();
}
