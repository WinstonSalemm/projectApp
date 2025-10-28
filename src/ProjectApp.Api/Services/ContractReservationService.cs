using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Services;

/// <summary>
/// Сервис для резервирования товаров по договорам и возврата при отмене
/// </summary>
public class ContractReservationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ContractReservationService> _logger;

    public ContractReservationService(AppDbContext db, ILogger<ContractReservationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Резервирует товар для позиции договора (списывает из партий)
    /// </summary>
    public async Task<List<ContractReservation>> ReserveItemAsync(ContractItem item, CancellationToken ct = default)
    {
        if (!item.ProductId.HasValue)
        {
            // Товара нет в каталоге - резервировать нечего
            _logger.LogInformation("Item {ItemId} has no ProductId - skipping reservation (future product)", item.Id);
            return new List<ContractReservation>();
        }

        var product = await _db.Products.FindAsync(new object[] { item.ProductId.Value }, ct);
        if (product == null)
            throw new InvalidOperationException($"Product {item.ProductId} not found");

        // Ищем доступные партии (FIFO - сначала старые)
        var batches = await _db.Batches
            .Where(b => b.ProductId == item.ProductId.Value && b.Qty > 0 && b.ArchivedAt == null)
            .OrderBy(b => b.CreatedAt)
            .ThenBy(b => b.Id)
            .ToListAsync(ct);

        if (!batches.Any())
            throw new InvalidOperationException($"No available batches for product {product.Name} (SKU: {product.Sku})");

        decimal qtyToReserve = item.Qty;
        var reservations = new List<ContractReservation>();

        foreach (var batch in batches)
        {
            if (qtyToReserve <= 0) break;

            decimal takeQty = Math.Min(batch.Qty, qtyToReserve);

            // Создаём резервацию
            var reservation = new ContractReservation
            {
                ContractItemId = item.Id,
                BatchId = batch.Id,
                ReservedQty = takeQty,
                CreatedAt = DateTime.UtcNow
            };

            _db.ContractReservations.Add(reservation);
            reservations.Add(reservation);

            // Списываем из партии
            batch.Qty -= takeQty;
            qtyToReserve -= takeQty;

            _logger.LogInformation("Reserved {Qty} of product {ProductId} from batch {BatchId} for contract item {ItemId}",
                takeQty, item.ProductId.Value, batch.Id, item.Id);
        }

        if (qtyToReserve > 0)
        {
            throw new InvalidOperationException(
                $"Not enough stock for product {product.Name} (SKU: {product.Sku}). Need {item.Qty}, available {item.Qty - qtyToReserve}");
        }

        // Обновляем Stock (ND-40 или IM-40 в зависимости от того откуда взяли)
        foreach (var reservation in reservations)
        {
            var batch = batches.First(b => b.Id == reservation.BatchId);
            var stock = await _db.Stocks.FindAsync(new object[] { item.ProductId.Value, batch.Register }, ct);
            if (stock != null)
            {
                stock.Qty -= reservation.ReservedQty;
            }
        }

        item.Status = ContractItemStatus.Reserved;
        await _db.SaveChangesAsync(ct);

        return reservations;
    }

    /// <summary>
    /// Отменяет договор - возвращает товар обратно в партии
    /// </summary>
    public async Task CancelContractAsync(int contractId, CancellationToken ct = default)
    {
        var contract = await _db.Contracts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == contractId, ct);

        if (contract == null)
            throw new InvalidOperationException($"Contract {contractId} not found");

        if (contract.Status == ContractStatus.Closed)
            throw new InvalidOperationException("Cannot cancel closed contract");

        if (contract.Status == ContractStatus.Cancelled)
            throw new InvalidOperationException("Contract already cancelled");

        // Получаем все резервации по этому договору
        var itemIds = contract.Items.Select(i => i.Id).ToList();
        var reservations = await _db.ContractReservations
            .Where(r => itemIds.Contains(r.ContractItemId) && r.ReturnedAt == null)
            .Include(r => r.Batch)
            .ToListAsync(ct);

        foreach (var reservation in reservations)
        {
            // Возвращаем количество обратно в партию
            var batch = reservation.Batch;
            batch.Qty += reservation.ReservedQty;

            // Обновляем Stock
            var stock = await _db.Stocks.FindAsync(new object[] { batch.ProductId, batch.Register }, ct);
            if (stock != null)
            {
                stock.Qty += reservation.ReservedQty;
            }

            // Отмечаем что резервация возвращена
            reservation.ReturnedAt = DateTime.UtcNow;

            _logger.LogInformation("Returned {Qty} to batch {BatchId} (product {ProductId}) from cancelled contract {ContractId}",
                reservation.ReservedQty, batch.Id, batch.ProductId, contractId);
        }

        // Обновляем статус позиций
        foreach (var item in contract.Items)
        {
            item.Status = ContractItemStatus.Cancelled;
        }

        // Обновляем статус договора
        contract.Status = ContractStatus.Cancelled;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Contract {ContractId} cancelled, {Count} reservations returned to stock",
            contractId, reservations.Count);
    }

    /// <summary>
    /// Отмечает позицию как отгруженную
    /// </summary>
    public async Task MarkItemAsShippedAsync(int contractItemId, decimal shippedQty, CancellationToken ct = default)
    {
        var item = await _db.ContractItems.FindAsync(new object[] { contractItemId }, ct);
        if (item == null)
            throw new InvalidOperationException($"Contract item {contractItemId} not found");

        if (item.DeliveredQty + shippedQty > item.Qty)
            throw new InvalidOperationException($"Cannot ship more than ordered quantity");

        item.DeliveredQty += shippedQty;

        if (item.DeliveredQty >= item.Qty)
        {
            item.Status = ContractItemStatus.Shipped;
        }

        await _db.SaveChangesAsync(ct);
    }
}
