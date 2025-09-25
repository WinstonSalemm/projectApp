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

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Check and deduct stocks and FIFO batches
        foreach (var it in sale.Items)
        {
            var stock = await _db.Stocks.FirstOrDefaultAsync(
                s => s.ProductId == it.ProductId && s.Register == register, ct);

            if (stock is null)
                throw new InvalidOperationException($"Stock record not found for ProductId={it.ProductId} Register={register}");

            var newQty = stock.Qty - it.Qty;
            if (newQty < 0)
                throw new InvalidOperationException($"Insufficient stock for ProductId={it.ProductId} in {register}. Available={stock.Qty}, Required={it.Qty}");

            // Deduct FIFO from batches
            await DeductFromBatchesAsync(it.ProductId, register, it.Qty, ct);

            stock.Qty = newQty;
        }

        _db.Sales.Add(sale);
        await _db.SaveChangesAsync(ct);

        // Create debt for Credit
        if (sale.PaymentType == PaymentType.Credit)
        {
            if (sale.ClientId is null)
                throw new InvalidOperationException("ClientId is required for Credit payments");

            var debt = new Debt
            {
                ClientId = sale.ClientId.Value,
                SaleId = sale.Id,
                Amount = sale.Total,
                DueDate = DateTime.UtcNow.AddDays(14),
                Status = DebtStatus.Open
            };
            _db.Debts.Add(debt);
            await _db.SaveChangesAsync(ct);
        }

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
            PaymentType.CashWithReceipt or PaymentType.CardWithReceipt or PaymentType.Site or PaymentType.Exchange => StockRegister.IM40,
            PaymentType.CashNoReceipt or PaymentType.Click or PaymentType.Payme => StockRegister.ND40,
            PaymentType.Credit => StockRegister.IM40,
            _ => StockRegister.IM40
        };
    }

    private async Task DeductFromBatchesAsync(int productId, StockRegister register, decimal qty, CancellationToken ct)
    {
        var remain = qty;
        var batches = await _db.Batches
            .Where(b => b.ProductId == productId && b.Register == register && b.Qty > 0)
            .OrderBy(b => b.CreatedAt).ThenBy(b => b.Id)
            .ToListAsync(ct);

        foreach (var b in batches)
        {
            if (remain <= 0) break;
            var take = Math.Min(b.Qty, remain);
            b.Qty -= take;
            remain -= take;
        }

        if (remain > 0)
            throw new InvalidOperationException($"FIFO batches are insufficient for ProductId={productId} in {register}. Missing={remain}");
    }
}
