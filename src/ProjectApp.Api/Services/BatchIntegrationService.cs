using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Services;

/// <summary>
/// Интеграция системы поставок с существующей системой партий (Batch)
/// При финализации расчета себестоимости создаёт партии товара
/// </summary>
public class BatchIntegrationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<BatchIntegrationService> _logger;

    public BatchIntegrationService(AppDbContext db, ILogger<BatchIntegrationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Создать партии товара из финализированной сессии расчета
    /// </summary>
    public async Task CreateBatchesFromCostingSession(
        int costingSessionId,
        CancellationToken ct = default)
    {
        // Загружаем сессию с данными
        var session = await _db.CostingSessions
            .Include(s => s.Supply)
            .Include(s => s.ItemSnapshots)
                .ThenInclude(snap => snap.SupplyItem)
                    .ThenInclude(item => item.Product)
            .FirstOrDefaultAsync(s => s.Id == costingSessionId, ct);

        if (session == null)
            throw new InvalidOperationException($"Costing session {costingSessionId} not found");

        if (!session.IsFinalized)
            throw new InvalidOperationException("Cannot create batches from non-finalized session");

        var supply = session.Supply;

        // Определяем регистр (ND-40 или IM-40)
        var register = supply.RegisterType == RegisterType.ND40 
            ? StockRegister.ND40 
            : StockRegister.IM40;

        var createdBatches = new List<Batch>();

        foreach (var snapshot in session.ItemSnapshots)
        {
            var supplyItem = snapshot.SupplyItem;
            var product = supplyItem.Product;

            // 1. Создаём партию (Batch) с рассчитанной себестоимостью
            var batch = new Batch
            {
                ProductId = product.Id,
                Register = register,
                Qty = snapshot.Quantity,
                UnitCost = snapshot.UnitCostUzs, // ГЛАВНОЕ: себестоимость из расчета!
                CreatedAt = DateTime.UtcNow,
                Code = supply.Code, // № ГТД
                Note = $"Supply {supply.Code}, calculated cost",
                SupplierName = $"Import (Costing Session #{session.Id})",
                InvoiceNumber = supply.Code,
                PurchaseDate = supply.CreatedAt,
                VatRate = session.VatPct, // НДС из сессии
                PurchaseSource = $"supply:{supply.Code}",
                GtdCode = supply.Code
            };

            _db.Batches.Add(batch);
            createdBatches.Add(batch);

            // 2. Обновляем остатки (Stock)
            var stock = await _db.Stocks
                .FirstOrDefaultAsync(s => s.ProductId == product.Id && s.Register == register, ct);

            if (stock == null)
            {
                stock = new Stock
                {
                    ProductId = product.Id,
                    Register = register,
                    Qty = 0
                };
                _db.Stocks.Add(stock);
            }

            stock.Qty += snapshot.Quantity;

            // 3. Создаём транзакцию инвентаря
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                ProductId = product.Id,
                Register = register,
                Type = InventoryTransactionType.Purchase,
                Qty = snapshot.Quantity,
                UnitCost = snapshot.UnitCostUzs,
                BatchId = null, // будет установлен после SaveChanges
                CreatedAt = DateTime.UtcNow,
                Note = $"Import from supply {supply.Code}, session #{session.Id}"
            });

            // 4. Обновляем себестоимость товара (Product.Cost)
            // Используем рассчитанную себестоимость
            product.Cost = snapshot.UnitCostUzs;

            _logger.LogInformation(
                "Created batch for product {ProductId} ({ProductName}), qty={Qty}, cost={Cost}",
                product.Id, product.Name, snapshot.Quantity, snapshot.UnitCostUzs);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created {Count} batches from costing session {SessionId} for supply {SupplyCode}",
            createdBatches.Count, session.Id, supply.Code);
    }

    /// <summary>
    /// Перевести партии поставки из ND-40 в IM-40
    /// </summary>
    public async Task TransferBatchesToIm40(
        int supplyId,
        CancellationToken ct = default)
    {
        var supply = await _db.Supplies
            .Include(s => s.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(s => s.Id == supplyId, ct);

        if (supply == null)
            throw new InvalidOperationException($"Supply {supplyId} not found");

        if (supply.RegisterType != RegisterType.ND40)
            throw new InvalidOperationException("Supply is not in ND-40");

        // Для каждой позиции поставки
        foreach (var item in supply.Items)
        {
            var productId = item.ProductId;
            var quantity = item.Quantity;

            // Находим партии ND-40 с кодом этой поставки
            var ndBatches = await _db.Batches
                .Where(b => b.ProductId == productId 
                    && b.Register == StockRegister.ND40 
                    && b.Code == supply.Code 
                    && b.Qty > 0)
                .OrderBy(b => b.CreatedAt)
                .ToListAsync(ct);

            decimal totalMoved = 0;

            foreach (var ndBatch in ndBatches)
            {
                if (totalMoved >= quantity) break;

                var qtyToMove = Math.Min(ndBatch.Qty, quantity - totalMoved);

                // Уменьшаем партию ND-40
                ndBatch.Qty -= qtyToMove;

                // Создаём партию IM-40 с той же себестоимостью
                var imBatch = new Batch
                {
                    ProductId = productId,
                    Register = StockRegister.IM40,
                    Qty = qtyToMove,
                    UnitCost = ndBatch.UnitCost, // сохраняем себестоимость!
                    CreatedAt = DateTime.UtcNow,
                    Code = ndBatch.Code,
                    Note = $"Transferred from ND-40, batch #{ndBatch.Id}",
                    SupplierName = ndBatch.SupplierName,
                    InvoiceNumber = ndBatch.InvoiceNumber,
                    PurchaseDate = ndBatch.PurchaseDate,
                    VatRate = ndBatch.VatRate,
                    PurchaseSource = $"transfer_nd_to_im:{supply.Code}",
                    GtdCode = ndBatch.GtdCode
                };

                _db.Batches.Add(imBatch);

                // Обновляем остатки
                var ndStock = await _db.Stocks
                    .FirstOrDefaultAsync(s => s.ProductId == productId && s.Register == StockRegister.ND40, ct);
                if (ndStock != null)
                    ndStock.Qty -= qtyToMove;

                var imStock = await _db.Stocks
                    .FirstOrDefaultAsync(s => s.ProductId == productId && s.Register == StockRegister.IM40, ct);
                if (imStock == null)
                {
                    imStock = new Stock { ProductId = productId, Register = StockRegister.IM40, Qty = 0 };
                    _db.Stocks.Add(imStock);
                }
                imStock.Qty += qtyToMove;

                // Транзакции
                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductId = productId,
                    Register = StockRegister.ND40,
                    Type = InventoryTransactionType.MoveNdToIm,
                    Qty = -qtyToMove,
                    UnitCost = ndBatch.UnitCost,
                    BatchId = ndBatch.Id,
                    CreatedAt = DateTime.UtcNow,
                    Note = $"Transfer ND→IM, supply {supply.Code}"
                });

                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductId = productId,
                    Register = StockRegister.IM40,
                    Type = InventoryTransactionType.MoveNdToIm,
                    Qty = qtyToMove,
                    UnitCost = ndBatch.UnitCost,
                    BatchId = null,
                    CreatedAt = DateTime.UtcNow,
                    Note = $"Transfer ND→IM, supply {supply.Code}"
                });

                totalMoved += qtyToMove;

                _logger.LogInformation(
                    "Transferred {Qty} of product {ProductId} from ND-40 to IM-40, supply {Code}",
                    qtyToMove, productId, supply.Code);
            }

            if (totalMoved < quantity)
            {
                _logger.LogWarning(
                    "Insufficient ND-40 stock for product {ProductId}. Requested={Requested}, Moved={Moved}",
                    productId, quantity, totalMoved);
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Transferred supply {SupplyId} ({Code}) from ND-40 to IM-40",
            supply.Id, supply.Code);
    }
}
