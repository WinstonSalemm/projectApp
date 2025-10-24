using ProjectApp.Api.Models;

namespace ProjectApp.Api.Services;

/// <summary>
/// Сервис для расчета себестоимости партий НД-40 по сложной формуле
/// </summary>
public class SupplyCostCalculationService
{
    // Значения по умолчанию из Excel
    private const decimal DEFAULT_EXCHANGE_RATE = 158.08m;
    private const decimal DEFAULT_CUSTOMS_FEE = 105000m;
    private const decimal DEFAULT_VAT_PERCENT = 22m;
    private const decimal DEFAULT_CORRECTION_PERCENT = 0.50m;
    private const decimal DEFAULT_SECURITY_PERCENT = 0.2m;
    private const decimal DEFAULT_DECLARATION_PERCENT = 1m;
    private const decimal DEFAULT_CERTIFICATION_PERCENT = 1m;
    private const decimal DEFAULT_CALCULATION_BASE = 10000000m;
    private const decimal DEFAULT_LOADING_PERCENT = 1.6m;

    /// <summary>
    /// Рассчитать себестоимость для позиции НД-40
    /// </summary>
    public SupplyCostCalculation Calculate(
        int productId,
        string productName,
        string? sku,
        decimal quantity,
        decimal priceRub,
        decimal? weight,
        decimal? exchangeRate = null,
        decimal? customsFee = null,
        decimal? vatPercent = null,
        decimal? correctionPercent = null,
        decimal? securityPercent = null,
        decimal? declarationPercent = null,
        decimal? certificationPercent = null,
        decimal? calculationBase = null,
        decimal? loadingPercent = null,
        string? createdBy = null,
        string? notes = null)
    {
        // Применяем значения по умолчанию
        var rate = exchangeRate ?? DEFAULT_EXCHANGE_RATE;
        var customs = customsFee ?? DEFAULT_CUSTOMS_FEE;
        var vat = vatPercent ?? DEFAULT_VAT_PERCENT;
        var correction = correctionPercent ?? DEFAULT_CORRECTION_PERCENT;
        var security = securityPercent ?? DEFAULT_SECURITY_PERCENT;
        var declaration = declarationPercent ?? DEFAULT_DECLARATION_PERCENT;
        var certification = certificationPercent ?? DEFAULT_CERTIFICATION_PERCENT;
        var calcBase = calculationBase ?? DEFAULT_CALCULATION_BASE;
        var loading = loadingPercent ?? DEFAULT_LOADING_PERCENT;

        // Расчет общей стоимости товара
        var priceTotal = quantity * priceRub;

        // Расчет таможни (пропорционально от общей суммы)
        var customsAmount = (priceTotal / calcBase) * customs;

        // Расчет НДС от таможни
        var vatAmount = (priceTotal + customsAmount) * (vat / 100m);

        // Расчет корректива
        var correctionAmount = priceTotal * (correction / 100m);

        // Расчет охраны
        var securityAmount = priceTotal * (security / 100m);

        // Расчет декларации
        var declarationAmount = priceTotal * (declaration / 100m);

        // Расчет сертификации
        var certificationAmount = priceTotal * (certification / 100m);

        // Расчет погрузки
        var loadingAmount = priceTotal * (loading / 100m);

        // ИТОГОВЫЙ СЕБЕС = сумма всех компонентов
        var totalCost = priceTotal + customsAmount + vatAmount + correctionAmount +
                       securityAmount + declarationAmount + certificationAmount + loadingAmount;

        // Себестоимость за единицу
        var unitCost = quantity > 0 ? totalCost / quantity : 0m;

        return new SupplyCostCalculation
        {
            // Глобальные параметры
            ExchangeRate = rate,
            CustomsFee = customs,
            VatPercent = vat,
            CorrectionPercent = correction,
            SecurityPercent = security,
            DeclarationPercent = declaration,
            CertificationPercent = certification,
            CalculationBase = calcBase,
            LoadingPercent = loading,

            // Параметры позиции
            ProductId = productId,
            ProductName = productName,
            Sku = sku,
            Quantity = quantity,
            PriceRub = priceRub,
            PriceTotal = priceTotal,
            Weight = weight,

            // Рассчитанные значения
            CustomsAmount = Math.Round(customsAmount, 2),
            VatAmount = Math.Round(vatAmount, 2),
            CorrectionAmount = Math.Round(correctionAmount, 2),
            SecurityAmount = Math.Round(securityAmount, 2),
            DeclarationAmount = Math.Round(declarationAmount, 2),
            CertificationAmount = Math.Round(certificationAmount, 2),
            LoadingAmount = Math.Round(loadingAmount, 2),
            DeviationAmount = null, // Может быть установлено вручную позже

            TotalCost = Math.Round(totalCost, 2),
            UnitCost = Math.Round(unitCost, 2),

            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            Notes = notes
        };
    }

    /// <summary>
    /// Получить значения по умолчанию для формы
    /// </summary>
    public Dictionary<string, decimal> GetDefaults()
    {
        return new Dictionary<string, decimal>
        {
            { "ExchangeRate", DEFAULT_EXCHANGE_RATE },
            { "CustomsFee", DEFAULT_CUSTOMS_FEE },
            { "VatPercent", DEFAULT_VAT_PERCENT },
            { "CorrectionPercent", DEFAULT_CORRECTION_PERCENT },
            { "SecurityPercent", DEFAULT_SECURITY_PERCENT },
            { "DeclarationPercent", DEFAULT_DECLARATION_PERCENT },
            { "CertificationPercent", DEFAULT_CERTIFICATION_PERCENT },
            { "CalculationBase", DEFAULT_CALCULATION_BASE },
            { "LoadingPercent", DEFAULT_LOADING_PERCENT }
        };
    }
}
