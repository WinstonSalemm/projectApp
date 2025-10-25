namespace ProjectApp.Api.Models;

/// <summary>
/// Расчет себестоимости партии товара
/// </summary>
public class BatchCostCalculation
{
    public int Id { get; set; }
    public int SupplyId { get; set; }                        // Поставка
    public int? BatchId { get; set; }                        // Партия (опционально, может быть null для новых товаров)
    public Batch? Batch { get; set; }
    
    // Базовые параметры
    public string ProductName { get; set; } = string.Empty;  // Наименование
    public int Quantity { get; set; }                        // Количество
    public decimal PriceRub { get; set; }                    // Цена в рублях (вручную)
    public decimal ExchangeRate { get; set; }                // Курс RUB
    public decimal PriceSom { get; set; }                    // Цена в сумах = PriceRub × ExchangeRate
    
    // НДС (записывается, но не участвует в расчетах)
    public decimal VatPercent { get; set; }                  // НДС % (пока не используется)
    
    // Доля от фиксированных сумм (рассчитывается автоматически)
    public decimal CustomsAmount { get; set; }               // Таможня (доля от общей суммы)
    public decimal ShippingAmount { get; set; }              // Погрузка (доля от общей суммы)
    
    // Проценты от "цены в сумах"
    public decimal LogisticsPercent { get; set; }            // Логистика %
    public decimal WarehousePercent { get; set; }            // Склад %
    public decimal DeclarationPercent { get; set; }          // Декларация %
    public decimal CertificationPercent { get; set; }        // Сертификация %
    public decimal MchsPercent { get; set; }                 // МЧС %
    public decimal DeviationPercent { get; set; }            // Отклонения %
    
    // Рассчитанные суммы по каждой статье (от PriceSom)
    public decimal LogisticsAmount { get; set; }             // = PriceSom × (LogisticsPercent / 100)
    public decimal WarehouseAmount { get; set; }             // = PriceSom × (WarehousePercent / 100)
    public decimal DeclarationAmount { get; set; }           // = PriceSom × (DeclarationPercent / 100)
    public decimal CertificationAmount { get; set; }         // = PriceSom × (CertificationPercent / 100)
    public decimal MchsAmount { get; set; }                  // = PriceSom × (MchsPercent / 100)
    public decimal DeviationAmount { get; set; }             // = PriceSom × (DeviationPercent / 100)
    
    // Итоги
    public decimal UnitCost { get; set; }                    // Себес закуп(итог) = PriceSom + все статьи
    public decimal TotalCost { get; set; }                   // Итого вся стоимость = UnitCost × Quantity
    
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Параметры расчета для всей партии (общие настройки)
/// </summary>
public class BatchCostSettings
{
    public int Id { get; set; }
    public int SupplyId { get; set; }
    
    // Глобальные параметры
    public decimal ExchangeRate { get; set; }              // Курс RUB по умолчанию
    
    // Фиксированные суммы на ВСЮ партию (делятся на все товары)
    public decimal CustomsFixedTotal { get; set; }         // Таможня - фикс. сумма в сумах
    public decimal ShippingFixedTotal { get; set; }        // Погрузка - фикс. сумма в сумах
    
    // Проценты по умолчанию для всех товаров
    public decimal DefaultVatPercent { get; set; }         // НДС % (не используется в расчетах)
    public decimal DefaultLogisticsPercent { get; set; }   // Логистика %
    public decimal DefaultWarehousePercent { get; set; }   // Склад %
    public decimal DefaultDeclarationPercent { get; set; } // Декларация %
    public decimal DefaultCertificationPercent { get; set; }// Сертификация %
    public decimal DefaultMchsPercent { get; set; }        // МЧС %
    public decimal DefaultDeviationPercent { get; set; }   // Отклонения %
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
