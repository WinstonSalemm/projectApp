using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Services;

public class ContractsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ContractsService> _logger;

    public ContractsService(AppDbContext db, ILogger<ContractsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Добавить оплату по договору
    /// </summary>
    public async Task<ContractPayment> AddPaymentAsync(int contractId, decimal amount, PaymentMethod method, string createdBy, string? note, CancellationToken ct = default)
    {
        var contract = await _db.Contracts
            .Include(c => c.Payments)
            .FirstOrDefaultAsync(c => c.Id == contractId, ct);

        if (contract == null)
            throw new InvalidOperationException($"Договор #{contractId} не найден");

        if (contract.Status == ContractStatus.Cancelled)
            throw new InvalidOperationException("Невозможно добавить оплату к отмененному договору");

        if (contract.Status == ContractStatus.Closed)
            throw new InvalidOperationException("Договор уже закрыт");

        var payment = new ContractPayment
        {
            ContractId = contractId,
            Amount = amount,
            Method = method,
            PaidAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            Note = note
        };

        _db.ContractPayments.Add(payment);
        contract.PaidAmount += amount;

        // Обновляем статус
        UpdateContractStatus(contract);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[ContractsService] Добавлена оплата {Amount} для договора #{ContractId}", amount, contractId);
        return payment;
    }

    /// <summary>
    /// Отгрузить товар по договору
    /// </summary>
    public async Task<ContractDelivery> DeliverItemAsync(int contractId, int contractItemId, decimal qty, string createdBy, string? note, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var contract = await _db.Contracts
            .Include(c => c.Items)
            .Include(c => c.Deliveries)
            .FirstOrDefaultAsync(c => c.Id == contractId, ct);

        if (contract == null)
            throw new InvalidOperationException($"Договор #{contractId} не найден");

        if (contract.Status == ContractStatus.Cancelled)
            throw new InvalidOperationException("Невозможно отгрузить товар по отмененному договору");

        if (contract.Status == ContractStatus.Closed)
            throw new InvalidOperationException("Договор уже закрыт");

        var contractItem = contract.Items.FirstOrDefault(i => i.Id == contractItemId);
        if (contractItem == null)
            throw new InvalidOperationException($"Позиция #{contractItemId} не найдена в договоре");

        if (contractItem.ProductId == null)
            throw new InvalidOperationException("Невозможно отгрузить товар без привязки к каталогу");

        var remainingQty = contractItem.Qty - contractItem.DeliveredQty;
        if (qty > remainingQty)
            throw new InvalidOperationException($"Запрошено {qty}, доступно {remainingQty}");

        // Списываем товар со склада по партиям FIFO (сначала ND-40, потом IM-40)
        var delivery = new ContractDelivery
        {
            ContractId = contractId,
            ContractItemId = contractItemId,
            ProductId = contractItem.ProductId.Value,
            Qty = qty,
            DeliveredAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            Note = note
        };

        // 1) Сначала пытаемся взять с ND-40
        var stockNd = await _db.Stocks.FirstOrDefaultAsync(
            s => s.ProductId == contractItem.ProductId && s.Register == StockRegister.ND40, ct);
        var availableNd = stockNd?.Qty ?? 0m;
        var takeNd = Math.Min(availableNd, qty);

        if (takeNd > 0)
        {
            var (batches, _) = await DeductFromBatchesAsync(contractItem.ProductId.Value, StockRegister.ND40, takeNd, ct);
            foreach (var b in batches)
            {
                delivery.Batches.Add(new ContractDeliveryBatch
                {
                    BatchId = b.batchId,
                    RegisterAtDelivery = StockRegister.ND40,
                    Qty = b.qty,
                    UnitCost = b.unitCost
                });
            }
            stockNd!.Qty -= takeNd;
        }

        // 2) Остаток берем с IM-40
        var remain = qty - takeNd;
        if (remain > 0)
        {
            var stockIm = await _db.Stocks.FirstOrDefaultAsync(
                s => s.ProductId == contractItem.ProductId && s.Register == StockRegister.IM40, ct);

            if (stockIm == null || stockIm.Qty < remain)
            {
                var available = stockIm?.Qty ?? 0m;
                throw new InvalidOperationException(
                    $"Недостаточно товара на складе. Требуется {remain}, доступно {available} в IM-40");
            }

            var (batches, _) = await DeductFromBatchesAsync(contractItem.ProductId.Value, StockRegister.IM40, remain, ct);
            foreach (var b in batches)
            {
                delivery.Batches.Add(new ContractDeliveryBatch
                {
                    BatchId = b.batchId,
                    RegisterAtDelivery = StockRegister.IM40,
                    Qty = b.qty,
                    UnitCost = b.unitCost
                });
            }
            stockIm.Qty -= remain;
        }

        _db.ContractDeliveries.Add(delivery);
        contractItem.DeliveredQty += qty;

        if (contractItem.IsDelivered)
            contract.DeliveredItemsCount++;

        // Обновляем статус договора
        UpdateContractStatus(contract);

        // Логируем транзакции
        foreach (var batch in delivery.Batches)
        {
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                ProductId = contractItem.ProductId.Value,
                Register = batch.RegisterAtDelivery,
                Type = InventoryTransactionType.ContractDelivery,
                Qty = -batch.Qty,
                UnitCost = batch.UnitCost,
                BatchId = batch.BatchId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy,
                Note = $"Отгрузка по договору #{contractId}, доставка #{delivery.Id}"
            });
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation("[ContractsService] Отгружено {Qty} товара #{ProductId} по договору #{ContractId}",
            qty, contractItem.ProductId, contractId);

        return delivery;
    }

    /// <summary>
    /// Закрыть договор (можно только если все партии в IM-40)
    /// </summary>
    public async Task<bool> CloseContractAsync(int contractId, string userName, CancellationToken ct = default)
    {
        var contract = await _db.Contracts
            .Include(c => c.Items)
            .Include(c => c.Deliveries)
                .ThenInclude(d => d.Batches)
            .FirstOrDefaultAsync(c => c.Id == contractId, ct);

        if (contract == null)
            throw new InvalidOperationException($"Договор #{contractId} не найден");

        if (contract.Status == ContractStatus.Closed)
            return true; // Уже закрыт

        if (contract.Status == ContractStatus.Cancelled)
            throw new InvalidOperationException("Невозможно закрыть отмененный договор");

        // Проверяем что полностью оплачен
        if (contract.PaidAmount < contract.TotalAmount)
            throw new InvalidOperationException($"Договор не полностью оплачен. Оплачено {contract.PaidAmount} из {contract.TotalAmount}");

        // Проверяем что все позиции отгружены (по количеству)
        if (contract.DeliveredItemsCount < contract.TotalItemsCount)
            throw new InvalidOperationException($"Не все позиции отгружены. Отгружено {contract.DeliveredItemsCount} из {contract.TotalItemsCount}");
        
        // Проверяем что полностью отгружено (по сумме)
        if (contract.ShippedAmount < contract.TotalAmount)
            throw new InvalidOperationException($"Не вся сумма отгружена. Отгружено {contract.ShippedAmount} из {contract.TotalAmount}");

        // Проверяем что все использованные партии теперь в IM-40
        var allBatchIds = contract.Deliveries.SelectMany(d => d.Batches).Select(b => b.BatchId).Distinct().ToList();
        var batches = await _db.Batches
            .Where(b => allBatchIds.Contains(b.Id))
            .ToListAsync(ct);

        var notInIm40 = batches.Where(b => b.Register != StockRegister.IM40).ToList();
        if (notInIm40.Any())
        {
            var batchIdsList = string.Join(", ", notInIm40.Select(b => b.Id));
            throw new InvalidOperationException(
                $"Невозможно закрыть договор: партии [{batchIdsList}] еще не переведены в IM-40");
        }

        contract.Status = ContractStatus.Closed;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[ContractsService] Договор #{ContractId} закрыт пользователем {UserName}", contractId, userName);
        return true;
    }

    /// <summary>
    /// Обновить статус договора на основе текущего состояния
    /// </summary>
    private void UpdateContractStatus(Contract contract)
    {
        if (contract.Status == ContractStatus.Cancelled || contract.Status == ContractStatus.Closed)
            return; // Финальные статусы не меняем

        var isPaid = contract.PaidAmount >= contract.TotalAmount;
        var isPartiallyPaid = contract.PaidAmount > 0 && contract.PaidAmount < contract.TotalAmount;
        var isDelivered = contract.DeliveredItemsCount >= contract.TotalItemsCount;
        var isPartiallyDelivered = contract.DeliveredItemsCount > 0 && contract.DeliveredItemsCount < contract.TotalItemsCount;

        if (isPaid && isDelivered)
        {
            // Оплачен и отгружен полностью, но не закрыт (партии могут быть еще в ND-40)
            contract.Status = ContractStatus.Delivered;
        }
        else if (isPaid && isPartiallyDelivered)
        {
            contract.Status = ContractStatus.PartiallyDelivered;
        }
        else if (isPaid)
        {
            contract.Status = ContractStatus.Paid;
        }
        else if (isPartiallyPaid && isDelivered)
        {
            contract.Status = ContractStatus.Delivered;
        }
        else if (isPartiallyPaid)
        {
            contract.Status = ContractStatus.PartiallyPaid;
        }
        else if (isDelivered)
        {
            contract.Status = ContractStatus.Delivered;
        }
        else if (isPartiallyDelivered)
        {
            contract.Status = ContractStatus.PartiallyDelivered;
        }
        else
        {
            contract.Status = ContractStatus.Active;
        }
    }

    /// <summary>
    /// Списать товар из партий по FIFO
    /// </summary>
    private async Task<(List<(int batchId, decimal qty, decimal unitCost)> batches, decimal avgCost)> DeductFromBatchesAsync(
        int productId, StockRegister register, decimal qty, CancellationToken ct)
    {
        var remain = qty;
        var totalCost = 0m;
        var result = new List<(int batchId, decimal qty, decimal unitCost)>();

        var batches = await _db.Batches
            .Where(b => b.ProductId == productId && b.Register == register && b.Qty > 0)
            .OrderBy(b => b.CreatedAt).ThenBy(b => b.Id)
            .ToListAsync(ct);

        foreach (var batch in batches)
        {
            if (remain <= 0) break;

            var take = Math.Min(batch.Qty, remain);
            if (take > 0)
            {
                totalCost += take * batch.UnitCost;
                batch.Qty -= take;
                remain -= take;
                result.Add((batch.Id, take, batch.UnitCost));
            }
        }

        if (remain > 0)
            throw new InvalidOperationException(
                $"Недостаточно партий для товара #{productId} в {register}. Не хватает {remain}");

        var avgCost = qty == 0 ? 0 : decimal.Round(totalCost / qty, 2, MidpointRounding.AwayFromZero);
        return (result, avgCost);
    }
}
