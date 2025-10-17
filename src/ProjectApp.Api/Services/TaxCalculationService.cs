using ProjectApp.Api.Data;
using ProjectApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ProjectApp.Api.Services;

/// <summary>
/// DTO для налогового отчета
/// </summary>
public class TaxReportDto
{
    public DateTime Period { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    
    // Выручка
    public decimal TotalRevenue { get; set; }              // С НДС
    public decimal RevenueWithoutVAT { get; set; }         // Без НДС
    public decimal VATFromSales { get; set; }              // НДС с продаж
    
    // Закупки
    public decimal TotalPurchases { get; set; }            // С НДС
    public decimal PurchasesWithoutVAT { get; set; }       // Без НДС
    public decimal VATFromPurchases { get; set; }          // НДС при закупке
    
    // НДС к уплате
    public decimal VATPayable { get; set; }                // НДС к уплате = НДС с продаж - НДС при закупке
    
    // Прибыль
    public decimal GrossProfit { get; set; }               // Валовая прибыль
    public decimal OperatingExpenses { get; set; }         // Операционные расходы
    public decimal EBIT { get; set; }                      // Прибыль до налогов
    
    // Налоги
    public decimal IncomeTax { get; set; }                 // Налог на прибыль
    public decimal SocialTax { get; set; }                 // Социальный налог
    public decimal INPS { get; set; }                      // ИНПС
    public decimal SchoolFund { get; set; }                // Школьный фонд
    public decimal TotalTaxes { get; set; }                // Всего налогов
    
    // Чистая прибыль
    public decimal NetProfit { get; set; }                 // Чистая прибыль после всех налогов
    public decimal NetProfitMargin { get; set; }           // Рентабельность (%)
    
    // Налоги к уплате
    public List<TaxPaymentDto> TaxesPayable { get; set; } = new();
}

public class TaxPaymentDto
{
    public TaxType Type { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsPaid { get; set; }
}

/// <summary>
/// Сервис для расчета налогов по законодательству Узбекистана
/// </summary>
public class TaxCalculationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TaxCalculationService> _logger;

    public TaxCalculationService(AppDbContext db, ILogger<TaxCalculationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Получить налоговые настройки
    /// </summary>
    public async Task<TaxSettings> GetTaxSettingsAsync()
    {
        var settings = await _db.Set<TaxSettings>().FirstOrDefaultAsync();
        if (settings == null)
        {
            // Создаем настройки по умолчанию
            settings = new TaxSettings
            {
                System = TaxSystem.General,
                VATRate = 12m,
                IncomeTaxRate = 15m,
                SocialTaxRate = 12m,
                INPSRate = 0.2m,
                SchoolFundRate = 1.5m,
                IsVATRegistered = true
            };
            _db.Set<TaxSettings>().Add(settings);
            await _db.SaveChangesAsync();
        }
        return settings;
    }

    /// <summary>
    /// Рассчитать НДС от суммы
    /// </summary>
    public decimal CalculateVAT(decimal amountWithVAT, decimal vatRate = 12m)
    {
        return amountWithVAT - (amountWithVAT / (1 + vatRate / 100m));
    }

    /// <summary>
    /// Получить сумму без НДС
    /// </summary>
    public decimal GetAmountWithoutVAT(decimal amountWithVAT, decimal vatRate = 12m)
    {
        return amountWithVAT / (1 + vatRate / 100m);
    }

    /// <summary>
    /// Добавить НДС к сумме
    /// </summary>
    public decimal AddVAT(decimal amountWithoutVAT, decimal vatRate = 12m)
    {
        return amountWithoutVAT * (1 + vatRate / 100m);
    }

    /// <summary>
    /// Рассчитать налоговый отчет за период
    /// </summary>
    public async Task<TaxReportDto> CalculateTaxReportAsync(DateTime from, DateTime to)
    {
        try
        {
            var settings = await GetTaxSettingsAsync();
            var report = new TaxReportDto
            {
                Period = from,
                PeriodName = $"{from:dd.MM.yyyy} - {to:dd.MM.yyyy}"
            };

            // 1. ВЫРУЧКА
            var sales = await _db.Sales
                .Where(s => s.CreatedAt >= from && s.CreatedAt < to)
                .ToListAsync();

            report.TotalRevenue = sales.Sum(s => s.Total);
            
            if (settings.IsVATRegistered)
            {
                report.RevenueWithoutVAT = GetAmountWithoutVAT(report.TotalRevenue, settings.VATRate);
                report.VATFromSales = CalculateVAT(report.TotalRevenue, settings.VATRate);
            }
            else
            {
                report.RevenueWithoutVAT = report.TotalRevenue;
                report.VATFromSales = 0m;
            }

            // 2. СЕБЕСТОИМОСТЬ (COGS)
            var cogs = await (from sale in _db.Sales
                             where sale.CreatedAt >= from && sale.CreatedAt < to
                             join saleItem in _db.SaleItems on sale.Id equals saleItem.SaleId
                             select (decimal?)(saleItem.Qty * saleItem.Cost)).SumAsync() ?? 0m;

            // НДС при закупке (входящий НДС) - предполагаем, что закупки тоже с НДС
            if (settings.IsVATRegistered)
            {
                report.TotalPurchases = AddVAT(cogs, settings.VATRate);
                report.PurchasesWithoutVAT = cogs;
                report.VATFromPurchases = CalculateVAT(report.TotalPurchases, settings.VATRate);
            }
            else
            {
                report.TotalPurchases = cogs;
                report.PurchasesWithoutVAT = cogs;
                report.VATFromPurchases = 0m;
            }

            // 3. НДС К УПЛАТЕ
            report.VATPayable = report.VATFromSales - report.VATFromPurchases;

            // 4. ВАЛОВАЯ ПРИБЫЛЬ
            report.GrossProfit = report.RevenueWithoutVAT - report.PurchasesWithoutVAT;

            // 5. ОПЕРАЦИОННЫЕ РАСХОДЫ
            var expenses = await _db.OperatingExpenses
                .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to && 
                           e.PaymentStatus == ExpensePaymentStatus.Paid)
                .SumAsync(e => (decimal?)e.Amount) ?? 0m;

            report.OperatingExpenses = expenses;

            // 6. ПРИБЫЛЬ ДО НАЛОГОВ (EBIT)
            report.EBIT = report.GrossProfit - report.OperatingExpenses;

            // 7. РАСЧЕТ НАЛОГОВ
            if (settings.System == TaxSystem.General)
            {
                // Общая система налогообложения
                
                // Налог на прибыль (15%)
                report.IncomeTax = report.EBIT > 0 ? report.EBIT * (settings.IncomeTaxRate / 100m) : 0m;
                
                // Социальные налоги (считаются от ФОТ)
                var salaryExpenses = await _db.OperatingExpenses
                    .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to && 
                               e.Type == ExpenseType.Salary &&
                               e.PaymentStatus == ExpensePaymentStatus.Paid)
                    .SumAsync(e => (decimal?)e.Amount) ?? 0m;

                report.SocialTax = salaryExpenses * (settings.SocialTaxRate / 100m);
                report.INPS = salaryExpenses * (settings.INPSRate / 100m);
                report.SchoolFund = salaryExpenses * (settings.SchoolFundRate / 100m);
            }
            else
            {
                // Упрощенная система налогообложения
                // Единый налог от выручки (4-7.5%)
                report.IncomeTax = report.TotalRevenue * (settings.SimplifiedTaxRate / 100m);
                report.SocialTax = 0m;
                report.INPS = 0m;
                report.SchoolFund = 0m;
                report.VATPayable = 0m; // На УСН нет НДС
            }

            // 8. ВСЕГО НАЛОГОВ
            report.TotalTaxes = report.VATPayable + report.IncomeTax + 
                               report.SocialTax + report.INPS + report.SchoolFund;

            // 9. ЧИСТАЯ ПРИБЫЛЬ
            report.NetProfit = report.EBIT - report.IncomeTax - report.SocialTax - 
                              report.INPS - report.SchoolFund;

            // 10. РЕНТАБЕЛЬНОСТЬ
            report.NetProfitMargin = report.TotalRevenue > 0 
                ? (report.NetProfit / report.TotalRevenue) * 100m 
                : 0m;

            // 11. НАЛОГИ К УПЛАТЕ
            report.TaxesPayable = new List<TaxPaymentDto>();

            if (report.VATPayable > 0)
            {
                report.TaxesPayable.Add(new TaxPaymentDto
                {
                    Type = TaxType.VAT,
                    TypeName = "НДС",
                    Amount = report.VATPayable,
                    DueDate = to.AddDays(20), // НДС платится до 20 числа следующего месяца
                    IsPaid = false
                });
            }

            if (report.IncomeTax > 0)
            {
                report.TaxesPayable.Add(new TaxPaymentDto
                {
                    Type = TaxType.IncomeTax,
                    TypeName = settings.System == TaxSystem.General ? "Налог на прибыль" : "Упрощенный налог",
                    Amount = report.IncomeTax,
                    DueDate = to.AddDays(25), // Налог на прибыль до 25 числа
                    IsPaid = false
                });
            }

            if (report.SocialTax > 0)
            {
                report.TaxesPayable.Add(new TaxPaymentDto
                {
                    Type = TaxType.SocialTax,
                    TypeName = "Единый социальный платеж",
                    Amount = report.SocialTax,
                    DueDate = to.AddDays(15),
                    IsPaid = false
                });
            }

            if (report.INPS > 0)
            {
                report.TaxesPayable.Add(new TaxPaymentDto
                {
                    Type = TaxType.INPS,
                    TypeName = "ИНПС",
                    Amount = report.INPS,
                    DueDate = to.AddDays(15),
                    IsPaid = false
                });
            }

            if (report.SchoolFund > 0)
            {
                report.TaxesPayable.Add(new TaxPaymentDto
                {
                    Type = TaxType.SchoolFund,
                    TypeName = "Школьный фонд",
                    Amount = report.SchoolFund,
                    DueDate = to.AddDays(15),
                    IsPaid = false
                });
            }

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка расчета налогового отчета");
            throw;
        }
    }

    /// <summary>
    /// Сохранить налоговые записи за период
    /// </summary>
    public async Task SaveTaxRecordsAsync(TaxReportDto report)
    {
        var period = new DateTime(report.Period.Year, report.Period.Month, 1);

        foreach (var tax in report.TaxesPayable)
        {
            var existing = await _db.Set<TaxRecord>()
                .FirstOrDefaultAsync(t => t.Period == period && t.Type == tax.Type);

            if (existing != null)
            {
                existing.TaxAmount = tax.Amount;
                existing.DueDate = tax.DueDate;
                existing.CalculatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.Set<TaxRecord>().Add(new TaxRecord
                {
                    Type = tax.Type,
                    Period = period,
                    TaxBase = 0m, // Можно детализировать
                    TaxRate = 0m,
                    TaxAmount = tax.Amount,
                    DueDate = tax.DueDate,
                    IsPaid = false
                });
            }
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Получить все неоплаченные налоги
    /// </summary>
    public async Task<List<TaxRecord>> GetUnpaidTaxesAsync()
    {
        return await _db.Set<TaxRecord>()
            .Where(t => !t.IsPaid)
            .OrderBy(t => t.DueDate)
            .ToListAsync();
    }

    /// <summary>
    /// Отметить налог как оплаченный
    /// </summary>
    public async Task MarkTaxAsPaidAsync(int taxRecordId)
    {
        var tax = await _db.Set<TaxRecord>().FindAsync(taxRecordId);
        if (tax != null)
        {
            tax.IsPaid = true;
            tax.PaidAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
