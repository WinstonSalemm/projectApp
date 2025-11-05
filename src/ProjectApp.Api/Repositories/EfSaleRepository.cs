using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Repositories;

public class EfSaleRepository : ISaleRepository
{
    private readonly AppDbContext _db;

    public EfSaleRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Sale> AddAsync(Sale sale, CancellationToken ct = default)
    {
        // Validate items
        if (sale.Items == null || sale.Items.Count == 0)
            throw new ArgumentException("Sale must have at least one item");

        var register = MapPaymentToRegister(sale.PaymentType);
        var isGreyPayment = sale.PaymentType == PaymentType.CashNoReceipt
                          || sale.PaymentType == PaymentType.ClickNoReceipt
                          || sale.PaymentType == PaymentType.Click // legacy mapping kept as grey
                          || sale.PaymentType == PaymentType.Payme
                          || sale.PaymentType == PaymentType.Debt;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

            // Will collect per-item consumption to persist after items get IDs
            var consumptionMap = new Dictionary<SaleItem, List<(int batchId, StockRegister reg, decimal qty)>>();
            // Check and deduct stocks and FIFO batches; compute COGS per item
            foreach (var it in sale.Items)
            {
                if (isGreyPayment)
                {
                    // 1) Сначала списываем с ND-40 (серый регистр)
                    var stockNd = await _db.Stocks.FirstOrDefaultAsync(
                        s => s.ProductId == it.ProductId && s.Register == StockRegister.ND40, ct);
                    var availableNd = stockNd?.Qty ?? 0m;
                    var takeNd = Math.Min(availableNd, it.Qty);

                    decimal costNd = 0m;
                    if (takeNd > 0)
                    {
                        var (avgNd, consNd) = await DeductFromBatchesAndComputeCostWithConsumptionAsync(it.ProductId, StockRegister.ND40, takeNd, ct);
                        costNd = avgNd;
                        if (!consumptionMap.ContainsKey(it)) consumptionMap[it] = new();
                        consumptionMap[it].AddRange(consNd.Select(c => (c.batchId, StockRegister.ND40, c.qty)));
                        stockNd!.Qty -= takeNd;
                    }

                    // 2) Остаток берем с IM-40 (белый регистр)
                    var remain = it.Qty - takeNd;
                    decimal costIm = 0m;
                    if (remain > 0)
                    {
                        var stockIm = await _db.Stocks.FirstOrDefaultAsync(
                            s => s.ProductId == it.ProductId && s.Register == StockRegister.IM40, ct);
                        if (stockIm is null)
                            throw new InvalidOperationException($"Запись остатка не найдена для товара ProductId={it.ProductId} в регистре {StockRegister.IM40}");

                        if (stockIm.Qty < remain)
                        {
                            var missingIm = remain - stockIm.Qty;
                            throw new InvalidOperationException($"Недостаточно остатков для товара ProductId={it.ProductId} в {StockRegister.IM40}. Доступно={stockIm.Qty}, Требуется={remain}, Не хватает={missingIm}");
                        }

                        var (avgIm, consIm) = await DeductFromBatchesAndComputeCostWithConsumptionAsync(it.ProductId, StockRegister.IM40, remain, ct);
                        costIm = avgIm;
                        if (!consumptionMap.ContainsKey(it)) consumptionMap[it] = new();
                        consumptionMap[it].AddRange(consIm.Select(c => (c.batchId, StockRegister.IM40, c.qty)));
                        stockIm.Qty -= remain;
                    }

                    // 3) Средневзвешенная себестоимость по обоим регистрам
                    var totalCost = costNd * takeNd + costIm * remain;
                    var avgCost = it.Qty == 0 ? 0 : decimal.Round(totalCost / it.Qty, 2, MidpointRounding.AwayFromZero);
                    it.Cost = avgCost;
                }
                else
                {
                    // Официальные оплаты: списываем только с IM-40
                    var stock = await _db.Stocks.FirstOrDefaultAsync(
                        s => s.ProductId == it.ProductId && s.Register == register, ct);

                    if (stock is null)
                        throw new InvalidOperationException($"Запись остатка не найдена для товара ProductId={it.ProductId} в регистре {register}");

                    var newQty = stock.Qty - it.Qty;
                    if (newQty < 0)
                    {
                        var missing = it.Qty - stock.Qty;
                        throw new InvalidOperationException($"Недостаточно остатков для товара ProductId={it.ProductId} в {register}. Доступно={stock.Qty}, Требуется={it.Qty}, Не хватает={missing}");
                    }

                    var (avgUnitCost, cons) = await DeductFromBatchesAndComputeCostWithConsumptionAsync(it.ProductId, register, it.Qty, ct);
                    if (!consumptionMap.ContainsKey(it)) consumptionMap[it] = new();
                    consumptionMap[it].AddRange(cons.Select(c => (c.batchId, register, c.qty)));
                    it.Cost = avgUnitCost;
                    stock.Qty = newQty;
                }
            }
            // Keep local consumption map to persist after items get IDs
            // We'll persist right after the first SaveChanges when SaleItems have IDs
            _pendingConsumptionMap = consumptionMap;

        _db.Sales.Add(sale);
        await _db.SaveChangesAsync(ct);

        // Persist per-batch consumption after items have IDs
        if (_pendingConsumptionMap is not null)
        {
            foreach (var si in sale.Items)
            {
                if (!_pendingConsumptionMap.TryGetValue(si, out var list)) continue;
                foreach (var e in list)
                {
                    _db.SaleItemConsumptions.Add(new SaleItemConsumption
                    {
                        SaleItemId = si.Id,
                        BatchId = e.batchId,
                        RegisterAtSale = e.reg,
                        Qty = e.qty
                    });
                    _db.InventoryTransactions.Add(new InventoryTransaction
                    {
                        ProductId = si.ProductId,
                        Register = e.reg,
                        Type = InventoryTransactionType.Sale,
                        Qty = -e.qty,
                        UnitCost = si.Cost,
                        BatchId = e.batchId,
                        SaleId = sale.Id,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = sale.CreatedBy,
                        Note = $"saleItem #{si.Id}"
                    });
                }
            }
            _pendingConsumptionMap = null;
            await _db.SaveChangesAsync(ct);
        }

        // Update per-manager stats (CreatedBy is username from JWT)
        var managerKey = string.IsNullOrWhiteSpace(sale.CreatedBy) ? "unknown" : sale.CreatedBy!;
        if (managerKey.Length > 64) managerKey = managerKey.Substring(0, 64);
        var stat = await _db.ManagerStats.FirstOrDefaultAsync(m => m.UserName == managerKey, ct);
        if (stat is null)
        {
            stat = new ManagerStat { UserName = managerKey, SalesCount = 1, Turnover = sale.Total };
            _db.ManagerStats.Add(stat);
        }
        else
        {
            stat.SalesCount += 1;
            stat.Turnover += sale.Total;
        }
        // Commissionable stats: only if client's OwnerUserName matches sale.CreatedBy
        if (sale.ClientId.HasValue)
        {
            var cli = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == sale.ClientId.Value, ct);
            if (cli != null && !string.IsNullOrWhiteSpace(cli.OwnerUserName)
                && string.Equals(cli.OwnerUserName, managerKey, StringComparison.OrdinalIgnoreCase))
            {
                stat.OwnedSalesCount += 1;
                stat.OwnedTurnover += sale.Total;
            }
        }
        await _db.SaveChangesAsync(ct);

        // No special post-processing for Reservation in current version

        await tx.CommitAsync(ct);
        return sale;
    }

    public async Task<Sale?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.Sales
            .AsNoTracking()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<IReadOnlyList<Sale>> QueryAsync(DateTime? dateFrom = null, DateTime? dateTo = null, string? createdBy = null, string? paymentType = null, int? clientId = null, CancellationToken ct = default)
    {
        var q = _db.Sales.AsNoTracking().AsQueryable();
        if (dateFrom.HasValue) q = q.Where(s => s.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue) q = q.Where(s => s.CreatedAt < dateTo.Value);
        if (!string.IsNullOrWhiteSpace(createdBy)) q = q.Where(s => s.CreatedBy == createdBy);
        if (clientId.HasValue) q = q.Where(s => s.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(paymentType))
        {
            if (Enum.TryParse<PaymentType>(paymentType, true, out var pt))
                q = q.Where(s => s.PaymentType == pt);
        }
        var list = await q.OrderByDescending(s => s.Id).ToListAsync(ct);
        return list;
    }

    private static StockRegister MapPaymentToRegister(PaymentType payment)
    {
        return payment switch
        {
            PaymentType.CashWithReceipt or PaymentType.CardWithReceipt or PaymentType.ClickWithReceipt or PaymentType.Site or PaymentType.Return or PaymentType.Contract or PaymentType.Reservation => StockRegister.IM40,
            PaymentType.CashNoReceipt or PaymentType.ClickNoReceipt or PaymentType.Click or PaymentType.Payme or PaymentType.Debt => StockRegister.ND40,
            _ => StockRegister.IM40
        };
    }

    // Local field to keep pending consumption until items get IDs
    private Dictionary<SaleItem, List<(int batchId, StockRegister reg, decimal qty)>>? _pendingConsumptionMap;

    private async Task<(decimal avgUnitCost, List<(int batchId, decimal qty)> consumption)> DeductFromBatchesAndComputeCostWithConsumptionAsync(int productId, StockRegister register, decimal qty, CancellationToken ct)
    {
        var remain = qty;
        var totalCost = 0m;
        var consumption = new List<(int batchId, decimal qty)>();
        var batches = await _db.Batches
            .Where(b => b.ProductId == productId && b.Register == register && b.Qty > 0)
            .OrderBy(b => b.CreatedAt).ThenBy(b => b.Id)
            .ToListAsync(ct);

        foreach (var b in batches)
        {
            if (remain <= 0) break;
            var take = Math.Min(b.Qty, remain);
            if (take > 0)
            {
                totalCost += take * b.UnitCost;
                b.Qty -= take;
                remain -= take;
                consumption.Add((b.Id, take));
            }
        }

        if (remain > 0)
            throw new InvalidOperationException($"FIFO batches are insufficient for ProductId={productId} in {register}. Missing={remain}");

        var avgUnitCost = qty == 0 ? 0 : decimal.Round(totalCost / qty, 2, MidpointRounding.AwayFromZero);
        return (avgUnitCost, consumption);
    }
}
