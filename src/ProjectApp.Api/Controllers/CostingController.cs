using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;
using ProjectApp.Api.Services;
using ProjectApp.Api.Costing.Dto;
using ProjectApp.Core.Costing.Models;
using ProjectApp.Core.Costing.Services;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/costing")]
[Authorize(Policy = "AdminOnly")]
public class CostingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CostingCalculationService _costingService;
    private readonly BatchIntegrationService _batchService;
    private readonly ILogger<CostingController> _logger;

    public CostingController(
        AppDbContext db, 
        CostingCalculationService costingService,
        BatchIntegrationService batchService,
        ILogger<CostingController> logger)
    {
        _db = db;
        _costingService = costingService;
        _batchService = batchService;
        _logger = logger;
    }

    /// <summary>
    /// Получить список сессий расчета для поставки
    /// </summary>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(IEnumerable<CostingSession>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessions([FromQuery] int? supplyId, CancellationToken ct)
    {
        var query = _db.CostingSessions.AsQueryable();

        if (supplyId.HasValue)
            query = query.Where(s => s.SupplyId == supplyId.Value);

        var sessions = await query
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        return Ok(sessions);
    }

    /// <summary>
    /// Получить детали сессии расчета со снапшотами
    /// </summary>
    [HttpGet("sessions/{id}")]
    [ProducesResponseType(typeof(CostingSessionWithSnapshots), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSession(int id, CancellationToken ct)
    {
        var session = await _db.CostingSessions
            .Include(s => s.Supply)
            .Include(s => s.ItemSnapshots)
            .ThenInclude(snap => snap.SupplyItem)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (session == null)
            return NotFound();

        return Ok(new CostingSessionWithSnapshots
        {
            Session = session,
            Snapshots = session.ItemSnapshots,
            GrandTotal = session.ItemSnapshots.Sum(s => s.TotalCostUzs)
        });
    }

    /// <summary>
    /// Создать новую сессию расчета с параметрами
    /// </summary>
    [HttpPost("sessions")]
    [ProducesResponseType(typeof(CostingSession), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSession([FromBody] CreateCostingSessionDto dto, CancellationToken ct)
    {
        var supply = await _db.Supplies.FindAsync(new object[] { dto.SupplyId }, ct);
        if (supply == null)
            return NotFound("Supply not found");

        // Валидация параметров
        if (dto.ExchangeRate <= 0)
            return BadRequest("ExchangeRate must be greater than 0");

        var session = new CostingSession
        {
            SupplyId = dto.SupplyId,
            ExchangeRate = dto.ExchangeRate,
            VatPct = dto.VatPct,
            LogisticsPct = dto.LogisticsPct,
            StoragePct = dto.StoragePct,
            DeclarationPct = dto.DeclarationPct,
            CertificationPct = dto.CertificationPct,
            MChsPct = dto.MChsPct,
            UnforeseenPct = dto.UnforeseenPct,
            CustomsFeeAbs = dto.CustomsFeeAbs,
            LoadingAbs = dto.LoadingAbs,
            ReturnsAbs = dto.ReturnsAbs,
            ApportionMethod = ApportionMethod.ByQuantity,
            IsFinalized = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.CostingSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetSession), new { id = session.Id }, session);
    }

    /// <summary>
    /// Пересчитать снапшоты для сессии
    /// </summary>
    [HttpPost("sessions/{id}/recalculate")]
    [ProducesResponseType(typeof(RecalculateResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Recalculate(int id, CancellationToken ct)
    {
        var session = await _db.CostingSessions
            .Include(s => s.Supply)
            .ThenInclude(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (session == null)
            return NotFound();

        if (session.IsFinalized)
            return BadRequest("Cannot recalculate finalized session");

        if (session.Supply.Items.Count == 0)
            return BadRequest("Supply has no items");

        // Удаляем старые снапшоты
        var oldSnapshots = await _db.CostingItemSnapshots
            .Where(s => s.CostingSessionId == id)
            .ToListAsync(ct);
        _db.CostingItemSnapshots.RemoveRange(oldSnapshots);

        // Рассчитываем новые снапшоты
        var newSnapshots = _costingService.Calculate(session, session.Supply.Items);

        // Проверяем инвариант
        var isValid = _costingService.ValidateAbsolutesSumInvariant(session, newSnapshots);
        if (!isValid)
        {
            _logger.LogWarning("Absolutes sum invariant validation failed for session {SessionId}", id);
        }

        // Сохраняем
        _db.CostingItemSnapshots.AddRange(newSnapshots);
        await _db.SaveChangesAsync(ct);

        return Ok(new RecalculateResult
        {
            Success = true,
            SnapshotsCount = newSnapshots.Count,
            GrandTotal = newSnapshots.Sum(s => s.TotalCostUzs),
            InvariantValid = isValid
        });
    }

    /// <summary>
    /// Зафиксировать расчет (после этого - только чтение)
    /// АВТОМАТИЧЕСКИ создаёт партии (Batch) с рассчитанной себестоимостью
    /// </summary>
    [HttpPost("sessions/{id}/finalize")]
    [ProducesResponseType(typeof(FinalizeResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Finalize(int id, CancellationToken ct)
    {
        var session = await _db.CostingSessions
            .Include(s => s.ItemSnapshots)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (session == null)
            return NotFound();

        if (session.IsFinalized)
            return BadRequest("Session already finalized");

        if (session.ItemSnapshots.Count == 0)
            return BadRequest("No snapshots calculated. Run recalculate first.");

        // 1. Финализируем сессию
        session.IsFinalized = true;
        await _db.SaveChangesAsync(ct);

        // 2. АВТОМАТИЧЕСКИ создаём партии с рассчитанной себестоимостью
        try
        {
            await _batchService.CreateBatchesFromCostingSession(id, ct);
            
            return Ok(new FinalizeResult
            {
                Success = true,
                Message = "Session finalized and batches created successfully",
                BatchesCreated = session.ItemSnapshots.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create batches for session {SessionId}", id);
            
            // Откатываем финализацию
            session.IsFinalized = false;
            await _db.SaveChangesAsync(ct);
            
            return BadRequest($"Failed to create batches: {ex.Message}");
        }
    }

    /// <summary>
    /// Предпросчет себестоимости по поставке (новый модуль). Все округления выполняются в калькуляторе (2 знака, AwayFromZero)
    /// </summary>
    [HttpPost("preview/{supplyId:int}")]
    [ProducesResponseType(typeof(CostingPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CostingPreviewDto>> Preview([FromRoute] int supplyId, [FromBody] CostingConfigDto cfgDto, CancellationToken ct)
    {
        var supplyExists = await _db.Supplies.AsNoTracking().AnyAsync(s => s.Id == supplyId, ct);
        if (!supplyExists) return NotFound();

        var items = await _db.SupplyItems
            .AsNoTracking()
            .Where(si => si.SupplyId == supplyId)
            .Select(si => new { si.Sku, si.Name, Qty = (decimal)si.Quantity, si.PriceRub, si.PriceUsd, si.PriceUzs })
            .ToListAsync(ct);

        var input = items.Select(x => new CostingInputRow
        {
            SkuOrName = string.IsNullOrWhiteSpace(x.Sku) ? x.Name : x.Sku,
            Quantity = x.Qty,
            PriceRub = x.PriceRub,
            PriceUsd = x.PriceUsd,
            PriceUzs = x.PriceUzs,
            LineTotalUzsOverride = null
        }).ToList();

        var cfg = new CostingConfig
        {
            RubToUzs = cfgDto.RubToUzs,
            UsdToUzs = cfgDto.UsdToUzs,
            CustomsFixedUzs = cfgDto.CustomsFixedUzs,
            LoadingTotalUzs = cfgDto.LoadingTotalUzs,
            LogisticsPct = cfgDto.LogisticsPct,
            WarehousePct = cfgDto.WarehousePct,
            DeclarationPct = cfgDto.DeclarationPct,
            CertificationPct = cfgDto.CertificationPct,
            McsPct = cfgDto.McsPct,
            DeviationPct = cfgDto.DeviationPct,
            TradeMarkupPct = cfgDto.TradeMarkupPct,
            VatPct = cfgDto.VatPct,
            ProfitTaxPct = cfgDto.ProfitTaxPct
        };

        var calc = new CostingCalculator();
        var res = calc.Calculate(input, cfg);

        var dto = new CostingPreviewDto
        {
            Rows = res.Rows.Select(r => new CostingRowDto
            {
                SkuOrName = r.SkuOrName,
                Quantity = r.Quantity,
                BasePriceUzs = r.BasePriceUzs,
                LineBaseTotalUzs = r.LineBaseTotalUzs,
                CustomsUzsPerUnit = r.CustomsUzsPerUnit,
                LoadingUzsPerUnit = r.LoadingUzsPerUnit,
                LogisticsUzsPerUnit = r.LogisticsUzsPerUnit,
                WarehouseUzsPerUnit = r.WarehouseUzsPerUnit,
                DeclarationUzsPerUnit = r.DeclarationUzsPerUnit,
                CertificationUzsPerUnit = r.CertificationUzsPerUnit,
                McsUzsPerUnit = r.McsUzsPerUnit,
                DeviationUzsPerUnit = r.DeviationUzsPerUnit,
                CostPerUnitUzs = r.CostPerUnitUzs,
                TradePriceUzs = r.TradePriceUzs,
                VatUzs = r.VatUzs,
                PriceWithVatUzs = r.PriceWithVatUzs,
                ProfitPerUnitUzs = r.ProfitPerUnitUzs,
                ProfitTaxUzs = r.ProfitTaxUzs,
                NetProfitUzs = r.NetProfitUzs
            }).ToList(),
            TotalQty = res.TotalQty,
            TotalBaseSumUzs = res.TotalBaseSumUzs,
            Warnings = res.Warnings
        };

        return Ok(dto);
    }
}

// DTOs
public record CreateCostingSessionDto(
    int SupplyId,
    decimal ExchangeRate,
    decimal VatPct,
    decimal LogisticsPct,
    decimal StoragePct,
    decimal DeclarationPct,
    decimal CertificationPct,
    decimal MChsPct,
    decimal UnforeseenPct,
    decimal CustomsFeeAbs,
    decimal LoadingAbs,
    decimal ReturnsAbs
);

public record CostingSessionWithSnapshots
{
    public CostingSession Session { get; init; } = null!;
    public List<CostingItemSnapshot> Snapshots { get; init; } = new();
    public decimal GrandTotal { get; init; }
}

public record RecalculateResult
{
    public bool Success { get; init; }
    public int SnapshotsCount { get; init; }
    public decimal GrandTotal { get; init; }
    public bool InvariantValid { get; init; }
}

public record FinalizeResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int BatchesCreated { get; init; }
}
