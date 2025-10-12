using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Services;

public enum ConsumptionStrategy { Fifo, Reverse }

public class InventoryConsumptionService
{
    private readonly AppDbContext _db;
    private readonly ILogger<InventoryConsumptionService> _logger;

    public InventoryConsumptionService(AppDbContext db, ILogger<InventoryConsumptionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<(decimal avgUnitCost, List<(int batchId, decimal qty)>)> ConsumeAsync(
        int productId,
        StockRegister register,
        decimal qty,
        ConsumptionStrategy strategy = ConsumptionStrategy.Fifo,
        decimal? overrideUnitCost = null,
        CancellationToken ct = default)
    {
        if (qty <= 0) throw new ArgumentException("Qty must be > 0", nameof(qty));
        var remain = qty;
        var totalCost = 0m;
        var consumption = new List<(int batchId, decimal qty)>();
        var qBatches = _db.Batches
            .Where(b => b.ProductId == productId && b.Register == register && b.Qty > 0);
        qBatches = strategy == ConsumptionStrategy.Fifo
            ? qBatches.OrderBy(b => b.CreatedAt).ThenBy(b => b.Id)
            : qBatches.OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id);
        var batches = await qBatches.ToListAsync(ct);

        foreach (var b in batches)
        {
            if (remain <= 0) break;
            var take = Math.Min(b.Qty, remain);
            if (take <= 0) continue;
            totalCost += take * b.UnitCost;
            b.Qty -= take;
            remain -= take;
            consumption.Add((b.Id, take));
        }

        if (remain > 0)
        {
            throw new InvalidOperationException($"Insufficient batches for ProductId={productId} in {register}. Missing={remain}");
        }

        var avg = qty == 0 ? 0 : decimal.Round((overrideUnitCost ?? (totalCost / qty)), 2, MidpointRounding.AwayFromZero);
        // Write inventory consumption audit rows (will persist once caller saves changes)
        var now = DateTime.UtcNow;
        foreach (var (batchId, takeQty) in consumption)
        {
            _db.InventoryConsumptions.Add(new InventoryConsumption
            {
                ProductId = productId,
                BatchId = batchId,
                Register = register,
                Qty = takeQty,
                CreatedAt = now
            });
        }
        return (avg, consumption);
    }

    public async Task OverrideCostAsync(int productId, decimal newUnitCost, string? note, CancellationToken ct = default)
    {
        // Record cost history; do not touch existing batches right away (explicit reprice may adjust)
        _db.ProductCostHistories.Add(new ProductCostHistory { ProductId = productId, UnitCost = newUnitCost, SnapshotAt = DateTime.UtcNow, Note = note });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> ReconcileAsync(CancellationToken ct = default)
    {
        // Ensure no negative batch quantities; clamp to zero and write log transactions Adjust
        var neg = await _db.Batches.Where(b => b.Qty < 0).ToListAsync(ct);
        foreach (var b in neg)
        {
            var delta = -b.Qty; // bring to zero
            b.Qty = 0;
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                ProductId = b.ProductId,
                Register = b.Register,
                Type = InventoryTransactionType.Adjust,
                Qty = -delta,
                UnitCost = b.UnitCost,
                BatchId = b.Id,
                CreatedAt = DateTime.UtcNow,
                Note = "reconcile: clamp negative to zero"
            });
        }
        await _db.SaveChangesAsync(ct);
        return neg.Count;
    }
}
