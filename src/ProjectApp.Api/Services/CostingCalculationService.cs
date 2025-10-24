using ProjectApp.Api.Models;

namespace ProjectApp.Api.Services;

/// <summary>
/// Сервис расчета себестоимости для поставок НД-40/ИМ-40
/// Формулы строго по ТЗ
/// </summary>
public class CostingCalculationService
{
    /// <summary>
    /// Рассчитать снапшоты для всех позиций поставки
    /// </summary>
    public List<CostingItemSnapshot> Calculate(
        CostingSession session,
        List<SupplyItem> items)
    {
        if (items == null || items.Count == 0)
            throw new ArgumentException("Items cannot be empty");

        var snapshots = new List<CostingItemSnapshot>();
        
        // Сначала считаем все без абсолютов
        foreach (var item in items)
        {
            var snapshot = new CostingItemSnapshot
            {
                CostingSessionId = session.Id,
                SupplyItemId = item.Id,
                Name = item.Name,
                Quantity = item.Quantity,
                PriceRub = item.PriceRub
            };

            // 1. PriceUzs = PriceRub * ExchangeRate
            snapshot.PriceUzs = item.PriceRub * session.ExchangeRate;

            // 2. Процентные статьи (всегда к PriceUzs)
            snapshot.VatUzs = snapshot.PriceUzs * session.VatPct;
            snapshot.LogisticsUzs = snapshot.PriceUzs * session.LogisticsPct;
            snapshot.StorageUzs = snapshot.PriceUzs * session.StoragePct;
            snapshot.DeclarationUzs = snapshot.PriceUzs * session.DeclarationPct;
            snapshot.CertificationUzs = snapshot.PriceUzs * session.CertificationPct;
            snapshot.MChsUzs = snapshot.PriceUzs * session.MChsPct;
            snapshot.UnforeseenUzs = snapshot.PriceUzs * session.UnforeseenPct;

            snapshots.Add(snapshot);
        }

        // 3. Распределение абсолютов по количеству (шт)
        var totalQuantity = items.Sum(i => i.Quantity);
        if (totalQuantity == 0)
            throw new ArgumentException("Total quantity cannot be zero");

        decimal customsSum = 0, loadingSum = 0, returnsSum = 0;

        for (int i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            var item = items[i];
            
            decimal share = (decimal)item.Quantity / totalQuantity;

            // Распределяем абсолюты
            snapshot.CustomsUzs = session.CustomsFeeAbs * share;
            snapshot.LoadingUzs = session.LoadingAbs * share;
            snapshot.ReturnsUzs = session.ReturnsAbs * share;

            // Округляем
            snapshot.CustomsUzs = Math.Round(snapshot.CustomsUzs, 4);
            snapshot.LoadingUzs = Math.Round(snapshot.LoadingUzs, 4);
            snapshot.ReturnsUzs = Math.Round(snapshot.ReturnsUzs, 4);

            customsSum += snapshot.CustomsUzs;
            loadingSum += snapshot.LoadingUzs;
            returnsSum += snapshot.ReturnsUzs;
        }

        // 4. Корректировка последней позиции (инвариант сумм)
        var lastSnapshot = snapshots.Last();
        lastSnapshot.CustomsUzs += session.CustomsFeeAbs - customsSum;
        lastSnapshot.LoadingUzs += session.LoadingAbs - loadingSum;
        lastSnapshot.ReturnsUzs += session.ReturnsAbs - returnsSum;

        // 5. Считаем итоги для каждой позиции
        foreach (var snapshot in snapshots)
        {
            // TotalCostUzs = PriceUzs + Σ(процентные) + Σ(абсолюты)
            snapshot.TotalCostUzs = 
                snapshot.PriceUzs +
                snapshot.VatUzs +
                snapshot.LogisticsUzs +
                snapshot.StorageUzs +
                snapshot.DeclarationUzs +
                snapshot.CertificationUzs +
                snapshot.MChsUzs +
                snapshot.UnforeseenUzs +
                snapshot.CustomsUzs +
                snapshot.LoadingUzs +
                snapshot.ReturnsUzs;

            // UnitCostUzs = TotalCostUzs / Quantity
            snapshot.UnitCostUzs = snapshot.TotalCostUzs / snapshot.Quantity;

            // Округление
            snapshot.TotalCostUzs = Math.Round(snapshot.TotalCostUzs, 4);
            snapshot.UnitCostUzs = Math.Round(snapshot.UnitCostUzs, 4);
        }

        return snapshots;
    }

    /// <summary>
    /// Проверка инварианта: сумма распределённых абсолютов == исходной
    /// </summary>
    public bool ValidateAbsolutesSumInvariant(
        CostingSession session,
        List<CostingItemSnapshot> snapshots)
    {
        var customsSum = snapshots.Sum(s => s.CustomsUzs);
        var loadingSum = snapshots.Sum(s => s.LoadingUzs);
        var returnsSum = snapshots.Sum(s => s.ReturnsUzs);

        const decimal tolerance = 0.01m; // допуск на округление

        return Math.Abs(customsSum - session.CustomsFeeAbs) < tolerance &&
               Math.Abs(loadingSum - session.LoadingAbs) < tolerance &&
               Math.Abs(returnsSum - session.ReturnsAbs) < tolerance;
    }
}
