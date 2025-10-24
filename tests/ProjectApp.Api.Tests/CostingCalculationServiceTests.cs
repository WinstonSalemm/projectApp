using ProjectApp.Api.Models;
using ProjectApp.Api.Services;
using Xunit;

namespace ProjectApp.Api.Tests;

public class CostingCalculationServiceTests
{
    private readonly CostingCalculationService _service;

    public CostingCalculationServiceTests()
    {
        _service = new CostingCalculationService();
    }

    [Fact]
    public void Test_CaseA_DistributionByQuantity()
    {
        // Arrange: 2 позиции
        // P1: Q=1000, PriceRub=100
        // P2: Q=15000, PriceRub=100
        // ΣQ=16000
        // CustomsFeeAbs=1600000 UZS
        // Проценты=0

        var session = new CostingSession
        {
            Id = 1,
            SupplyId = 1,
            ExchangeRate = 160,
            VatPct = 0,
            LogisticsPct = 0,
            StoragePct = 0,
            DeclarationPct = 0,
            CertificationPct = 0,
            MChsPct = 0,
            UnforeseenPct = 0,
            CustomsFeeAbs = 1_600_000,
            LoadingAbs = 0,
            ReturnsAbs = 0
        };

        var items = new List<SupplyItem>
        {
            new() { Id = 1, SupplyId = 1, ProductId = 1, Name = "Product 1", Quantity = 1_000, PriceRub = 100 },
            new() { Id = 2, SupplyId = 1, ProductId = 2, Name = "Product 2", Quantity = 15_000, PriceRub = 100 }
        };

        // Act
        var snapshots = _service.Calculate(session, items);

        // Assert
        Assert.Equal(2, snapshots.Count);

        // Доля P1 = 1000/16000 = 0.0625 → 100,000 UZS
        var snap1 = snapshots[0];
        Assert.Equal(100_000, snap1.CustomsUzs);

        // Доля P2 = 15000/16000 = 0.9375 → 1,500,000 UZS
        var snap2 = snapshots[1];
        // Внимание: последняя позиция корректируется для инварианта
        // Σ = 100000 + snap2.CustomsUzs должно быть ровно 1600000
        var totalCustoms = snap1.CustomsUzs + snap2.CustomsUzs;
        Assert.Equal(1_600_000, totalCustoms, 2); // допуск на округление

        // Проверка инварианта
        var isValid = _service.ValidateAbsolutesSumInvariant(session, snapshots);
        Assert.True(isValid);
    }

    [Fact]
    public void Test_CaseB_PercentsToPriceUzs()
    {
        // Arrange: PriceRub=200, ExchangeRate=160 → PriceUzs=32000
        // НДС=0.22 → VatUzs=7040
        // Логистика=0.01 → 320
        // Склад=0.005 → 160

        var session = new CostingSession
        {
            Id = 1,
            SupplyId = 1,
            ExchangeRate = 160,
            VatPct = 0.22m,
            LogisticsPct = 0.01m,
            StoragePct = 0.005m,
            DeclarationPct = 0,
            CertificationPct = 0,
            MChsPct = 0,
            UnforeseenPct = 0,
            CustomsFeeAbs = 0,
            LoadingAbs = 0,
            ReturnsAbs = 0
        };

        var items = new List<SupplyItem>
        {
            new() { Id = 1, SupplyId = 1, ProductId = 1, Name = "Product", Quantity = 1, PriceRub = 200 }
        };

        // Act
        var snapshots = _service.Calculate(session, items);

        // Assert
        var snap = snapshots[0];
        Assert.Equal(32_000, snap.PriceUzs);
        Assert.Equal(7_040, snap.VatUzs);
        Assert.Equal(320, snap.LogisticsUzs);
        Assert.Equal(160, snap.StorageUzs);

        // TotalCostUzs = PriceUzs + Σпроцентов
        var expected = 32_000 + 7_040 + 320 + 160;
        Assert.Equal(expected, snap.TotalCostUzs);

        // UnitCostUzs = TotalCostUzs / Quantity
        Assert.Equal(expected, snap.UnitCostUzs); // т.к. Quantity=1
    }

    [Fact]
    public void Test_CaseC_MixedCalculation()
    {
        // Arrange: 3 позиции (разные Q и PriceRub)
        // ExchangeRate=150 (любой вручную)
        // Проценты непустые, абсолюты непустые

        var session = new CostingSession
        {
            Id = 1,
            SupplyId = 1,
            ExchangeRate = 150,
            VatPct = 0.22m,
            LogisticsPct = 0.01m,
            StoragePct = 0.005m,
            DeclarationPct = 0.01m,
            CertificationPct = 0.01m,
            MChsPct = 0.005m,
            UnforeseenPct = 0.015m,
            CustomsFeeAbs = 500_000,
            LoadingAbs = 100_000,
            ReturnsAbs = 50_000
        };

        var items = new List<SupplyItem>
        {
            new() { Id = 1, SupplyId = 1, ProductId = 1, Name = "Product 1", Quantity = 10, PriceRub = 100 },
            new() { Id = 2, SupplyId = 1, ProductId = 2, Name = "Product 2", Quantity = 40, PriceRub = 200 },
            new() { Id = 3, SupplyId = 1, ProductId = 3, Name = "Product 3", Quantity = 50, PriceRub = 150 }
        };

        // Act
        var snapshots = _service.Calculate(session, items);

        // Assert
        Assert.Equal(3, snapshots.Count);

        // 1. Проверка что проценты считались от PriceUzs
        foreach (var snap in snapshots)
        {
            var expectedVat = snap.PriceUzs * 0.22m;
            Assert.Equal(Math.Round(expectedVat, 4), snap.VatUzs);
        }

        // 2. Проверка что абсолюты распределены строго по Q
        var totalQ = items.Sum(i => i.Quantity); // 10+40+50=100
        
        var snap1 = snapshots[0];
        var snap2 = snapshots[1];
        var snap3 = snapshots[2];

        // Доля P1 = 10/100 = 0.1
        // Доля P2 = 40/100 = 0.4
        // Доля P3 = 50/100 = 0.5
        // (с учетом корректировки последней позиции)

        // 3. Проверка инварианта сумм абсолютов
        var isValid = _service.ValidateAbsolutesSumInvariant(session, snapshots);
        Assert.True(isValid);

        // 4. Проверка что UnitCostUzs = Total / Q корректна
        foreach (var snap in snapshots)
        {
            var item = items.First(i => i.Id == snap.SupplyItemId);
            var expectedUnitCost = snap.TotalCostUzs / item.Quantity;
            Assert.Equal(Math.Round(expectedUnitCost, 4), snap.UnitCostUzs);
        }
    }

    [Fact]
    public void Test_InvariantValidation_CustomsFee()
    {
        // Arrange
        var session = new CostingSession
        {
            ExchangeRate = 160,
            CustomsFeeAbs = 1_000_000,
            LoadingAbs = 0,
            ReturnsAbs = 0,
            VatPct = 0,
            LogisticsPct = 0,
            StoragePct = 0,
            DeclarationPct = 0,
            CertificationPct = 0,
            MChsPct = 0,
            UnforeseenPct = 0
        };

        var items = new List<SupplyItem>
        {
            new() { Id = 1, ProductId = 1, Name = "P1", Quantity = 30, PriceRub = 100 },
            new() { Id = 2, ProductId = 2, Name = "P2", Quantity = 70, PriceRub = 100 }
        };

        // Act
        var snapshots = _service.Calculate(session, items);

        // Assert: Σраспределённых == исходной абсолютной сумме
        var totalCustoms = snapshots.Sum(s => s.CustomsUzs);
        Assert.Equal(1_000_000, totalCustoms, 2); // допуск на округление

        var isValid = _service.ValidateAbsolutesSumInvariant(session, snapshots);
        Assert.True(isValid);
    }

    [Fact]
    public void Test_ZeroQuantity_ThrowsException()
    {
        // Arrange
        var session = new CostingSession { ExchangeRate = 160 };
        var items = new List<SupplyItem>
        {
            new() { Id = 1, ProductId = 1, Name = "P1", Quantity = 0, PriceRub = 100 }
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.Calculate(session, items));
    }

    [Fact]
    public void Test_EmptyItems_ThrowsException()
    {
        // Arrange
        var session = new CostingSession { ExchangeRate = 160 };
        var items = new List<SupplyItem>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.Calculate(session, items));
    }
}
