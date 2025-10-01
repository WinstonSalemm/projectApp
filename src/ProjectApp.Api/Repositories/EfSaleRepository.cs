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
                          || sale.PaymentType == PaymentType.Payme;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Reservation: не списываем остатки (фиксируем бронь), себестоимость = 0
        if (sale.PaymentType == PaymentType.Reservation)
        {
            foreach (var it in sale.Items)
                it.Cost = 0m;
        }
        else
        {
            // Check and deduct stocks and FIFO batches; compute COGS per item
            foreach (var it in sale.Items)
            {
                if (isGreyPayment)
                {
                    // 1) Забираем с ND-40 максимум возможного
                    var stockNd = await _db.Stocks.FirstOrDefaultAsync(
                        s => s.ProductId == it.ProductId && s.Register == StockRegister.ND40, ct);
                    var availableNd = stockNd?.Qty ?? 0m;
                    var takeNd = Math.Min(availableNd, it.Qty);

                    decimal costNd = 0m;
                    if (takeNd > 0)
                    {
                        costNd = await DeductFromBatchesAndComputeCostAsync(it.ProductId, StockRegister.ND40, takeNd, ct);
                        stockNd!.Qty -= takeNd;
                    }

                    // 2) Остаток — с IM-40
                    var remain = it.Qty - takeNd;
                    decimal costIm = 0m;
                    if (remain > 0)
                    {
                        var stockIm = await _db.Stocks.FirstOrDefaultAsync(
                            s => s.ProductId == it.ProductId && s.Register == StockRegister.IM40, ct);
                        if (stockIm is null)
                            throw new InvalidOperationException($"Stock record not found for ProductId={it.ProductId} Register={StockRegister.IM40}");

                        if (stockIm.Qty < remain)
                            throw new InvalidOperationException($"Insufficient stock for ProductId={it.ProductId} in {StockRegister.IM40}. Available={stockIm.Qty}, Required={remain}");

                        costIm = await DeductFromBatchesAndComputeCostAsync(it.ProductId, StockRegister.IM40, remain, ct);
                        stockIm.Qty -= remain;
                    }

                    // 3) Средневзвешенная себестоимость по всем списанным регистрам
                    var totalCost = costNd * takeNd + costIm * (it.Qty - takeNd);
                    var avg = it.Qty == 0 ? 0 : decimal.Round(totalCost / it.Qty, 2, MidpointRounding.AwayFromZero);
                    it.Cost = avg;
                }
                else
                {
                    // Официальные оплаты: списываем только с IM-40
                    var stock = await _db.Stocks.FirstOrDefaultAsync(
                        s => s.ProductId == it.ProductId && s.Register == register, ct);

                    if (stock is null)
                        throw new InvalidOperationException($"Stock record not found for ProductId={it.ProductId} Register={register}");

                    var newQty = stock.Qty - it.Qty;
                    if (newQty < 0)
                        throw new InvalidOperationException($"Insufficient stock for ProductId={it.ProductId} in {register}. Available={stock.Qty}, Required={it.Qty}");

                    var avgUnitCost = await DeductFromBatchesAndComputeCostAsync(it.ProductId, register, it.Qty, ct);
                    it.Cost = avgUnitCost;
                    stock.Qty = newQty;
                }
            }
        }

        _db.Sales.Add(sale);
        await _db.SaveChangesAsync(ct);

        // Update per-manager stats (CreatedBy is username from JWT)
        var managerKey = string.IsNullOrWhiteSpace(sale.CreatedBy) ? "unknown" : sale.CreatedBy!;
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

    private static StockRegister MapPaymentToRegister(PaymentType payment)
    {
        return payment switch
        {
            PaymentType.CashWithReceipt or PaymentType.CardWithReceipt or PaymentType.ClickWithReceipt or PaymentType.Site or PaymentType.Return => StockRegister.IM40,
            PaymentType.CashNoReceipt or PaymentType.ClickNoReceipt or PaymentType.Click or PaymentType.Payme or PaymentType.Reservation => StockRegister.ND40,
            _ => StockRegister.IM40
        };
    }

    private async Task<decimal> DeductFromBatchesAndComputeCostAsync(int productId, StockRegister register, decimal qty, CancellationToken ct)
    {
        var remain = qty;
        var totalCost = 0m;
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
            }
        }

        if (remain > 0)
            throw new InvalidOperationException($"FIFO batches are insufficient for ProductId={productId} in {register}. Missing={remain}");

        var avgUnitCost = qty == 0 ? 0 : decimal.Round(totalCost / qty, 2, MidpointRounding.AwayFromZero);
        return avgUnitCost;
    }
}
