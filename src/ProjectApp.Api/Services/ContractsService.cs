using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Services;

public class ContractsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ContractsService> _logger;
    private readonly BatchIntegrationService _batchService;

    public ContractsService(AppDbContext db, ILogger<ContractsService> logger, BatchIntegrationService batchService)
    {
        _db = db;
        _logger = logger;
        _batchService = batchService;
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

        // Списание товара со склада по ПОЛИТИКЕ: IM-40 сначала; при нехватке — конвертировать недостающий объём из ND-40 в IM-40 и списывать из IM-40
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

        // 1) Проверяем доступный остаток в IM-40
        var stockIm = await _db.Stocks.FirstOrDefaultAsync(
            s => s.ProductId == contractItem.ProductId && s.Register == StockRegister.IM40, ct);
        var imAvailable = stockIm?.Qty ?? 0m;

        // 2) Если IM-40 недостаточно — пробуем сконвертировать недостающее из ND-40 в IM-40 через интеграционный сервис
        if (imAvailable < qty)
        {
            var missing = qty - imAvailable;
            try
            {
                var moved = await _batchService.ConvertProductNdToImQuantity(contractItem.ProductId.Value, missing, ct);

                // Обновляем доступный остаток после конверсии
                if (stockIm == null)
                {
                    stockIm = await _db.Stocks.FirstOrDefaultAsync(
                        s => s.ProductId == contractItem.ProductId && s.Register == StockRegister.IM40, ct);
                }
                imAvailable = stockIm?.Qty ?? 0m;

                if (imAvailable < qty)
                {
                    // Недостаточно даже после успешной частичной конверсии — переводим в ожидание конверсии
                    delivery.Status = ShipmentStatus.PendingConversion;
                    delivery.MissingQtyForConversion = qty - imAvailable;
                    delivery.UnitPrice = contractItem.UnitPrice;
                    _db.ContractDeliveries.Add(delivery);
                    await _db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    _logger.LogWarning("[ContractsService] Недостаточно IM-40 после конверсии (moved={Moved}). Отгрузка помечена как PendingConversion", moved);
                    return delivery;
                }
            }
            catch (Exception ex)
            {
                // Сервис конверсии недоступен — создаем Pending запись без списаний
                delivery.Status = ShipmentStatus.PendingConversion;
                delivery.MissingQtyForConversion = missing;
                delivery.UnitPrice = contractItem.UnitPrice;
                _db.ContractDeliveries.Add(delivery);
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                _logger.LogError(ex, "[ContractsService] Конверсия ND→IM недоступна. Отгрузка в статусе PendingConversion");
                return delivery;
            }
        }

        // 3) Списываем требуемое количество ТОЛЬКО из IM-40 (после возможной конверсии)
        var (imBatches, _) = await DeductFromBatchesAsync(contractItem.ProductId.Value, StockRegister.IM40, qty, ct);
        foreach (var b in imBatches)
        {
            delivery.Batches.Add(new ContractDeliveryBatch
            {
                BatchId = b.batchId,
                RegisterAtDelivery = StockRegister.IM40,
                Qty = b.qty,
                UnitCost = b.unitCost
            });
        }
        if (stockIm != null)
            stockIm.Qty -= qty;

        _db.ContractDeliveries.Add(delivery);
        delivery.Status = ShipmentStatus.Completed;
        delivery.UnitPrice = contractItem.UnitPrice;
        contractItem.DeliveredQty += qty;
        if (contractItem.IsDelivered) contract.DeliveredItemsCount++;
        contract.ShippedAmount += qty * contractItem.UnitPrice; // стоимость по цене договора

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
    /// Отменить отгрузку: вернуть товар на склад и откатить суммы по договору
    /// </summary>
    public async Task<bool> CancelDeliveryAsync(int contractId, int deliveryId, string userName, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var contract = await _db.Contracts
            .Include(c => c.Items)
            .Include(c => c.Deliveries)
                .ThenInclude(d => d.Batches)
            .FirstOrDefaultAsync(c => c.Id == contractId, ct);

        if (contract == null)
            throw new InvalidOperationException($"Договор #{contractId} не найден");
        if (contract.Status == ContractStatus.Cancelled)
            throw new InvalidOperationException("Невозможно отменить отгрузку в отмененном договоре");
        if (contract.Status == ContractStatus.Closed)
            throw new InvalidOperationException("Договор закрыт");

        var delivery = contract.Deliveries.FirstOrDefault(d => d.Id == deliveryId);
        if (delivery == null)
            throw new InvalidOperationException($"Отгрузка #{deliveryId} не найдена");

        var item = contract.Items.FirstOrDefault(i => i.Id == delivery.ContractItemId);
        if (item == null)
            throw new InvalidOperationException($"Позиция #{delivery.ContractItemId} не найдена");

        // Если отгрузка в ожидании конверсии — просто удаляем запись
        if (delivery.Status == ShipmentStatus.PendingConversion)
        {
            _db.ContractDeliveries.Remove(delivery);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            _logger.LogInformation("[ContractsService] Удалена Pending отгрузка #{DeliveryId} по договору #{ContractId}", deliveryId, contractId);
            return true;
        }

        // Возвращаем по партиям
        foreach (var b in delivery.Batches)
        {
            // Восстанавливаем партии
            var batch = await _db.Batches.FirstOrDefaultAsync(x => x.Id == b.BatchId, ct);
            if (batch != null)
            {
                batch.Qty += b.Qty;
            }

            // Восстанавливаем складские остатки
            var stock = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == item.ProductId && s.Register == b.RegisterAtDelivery, ct);
            if (stock == null)
            {
                stock = new Stock { ProductId = item.ProductId!.Value, Register = b.RegisterAtDelivery, Qty = 0 };
                _db.Stocks.Add(stock);
            }
            stock.Qty += b.Qty;

            // Транзакция коррекции
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                ProductId = item.ProductId!.Value,
                Register = b.RegisterAtDelivery,
                Type = InventoryTransactionType.Adjust,
                Qty = b.Qty,
                UnitCost = b.UnitCost,
                BatchId = b.BatchId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName,
                Note = $"Отмена отгрузки #{delivery.Id} по договору #{contractId}"
            });
        }

        // Откатываем показатели позиции/договора
        var wasDelivered = item.IsDelivered;
        item.DeliveredQty -= delivery.Qty;
        if (item.DeliveredQty < 0) item.DeliveredQty = 0;
        if (wasDelivered && !item.IsDelivered && contract.DeliveredItemsCount > 0)
            contract.DeliveredItemsCount--;
        contract.ShippedAmount -= delivery.Qty * item.UnitPrice;
        if (contract.ShippedAmount < 0) contract.ShippedAmount = 0;

        // Удаляем Delivery и его батчи
        _db.ContractDeliveryBatches.RemoveRange(delivery.Batches);
        _db.ContractDeliveries.Remove(delivery);

        // Обновляем статус
        UpdateContractStatus(contract);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation("[ContractsService] Отмена отгрузки #{DeliveryId} по договору #{ContractId}", deliveryId, contractId);
        return true;
    }

    /// <summary>
    /// Повторить конверсию для Pending-отгрузки и завершить списание при успехе
    /// </summary>
    public async Task<ContractDelivery> RetryPendingDeliveryAsync(int contractId, int deliveryId, string userName, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var delivery = await _db.ContractDeliveries.FirstOrDefaultAsync(d => d.Id == deliveryId && d.ContractId == contractId, ct);
        if (delivery == null)
            throw new InvalidOperationException($"Отгрузка #{deliveryId} не найдена");
        if (delivery.Status != ShipmentStatus.PendingConversion)
            throw new InvalidOperationException("Отгрузка не в статусе 'Ожидает конверсии'");

        var item = await _db.ContractItems.FirstOrDefaultAsync(i => i.Id == delivery.ContractItemId, ct);
        if (item == null) throw new InvalidOperationException("Позиция договора не найдена");

        var contract = await _db.Contracts.FirstOrDefaultAsync(c => c.Id == contractId, ct);
        if (contract == null) throw new InvalidOperationException("Договор не найден");

        var missing = delivery.MissingQtyForConversion ?? delivery.Qty;
        try
        {
            var moved = await _batchService.ConvertProductNdToImQuantity(delivery.ProductId, missing, ct);
            delivery.RetryCount += 1;
            delivery.LastRetryAt = DateTime.UtcNow;

            // Проверяем доступность IM-40 теперь
            var stockIm = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == delivery.ProductId && s.Register == StockRegister.IM40, ct);
            var imAvailable = stockIm?.Qty ?? 0m;
            if (imAvailable < delivery.Qty)
            {
                // Остались хвосты — остается pending
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return delivery;
            }

            // Достаточно — списываем из IM-40 и завершаем
            var (batches, _) = await DeductFromBatchesAsync(delivery.ProductId, StockRegister.IM40, delivery.Qty, ct);
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
            if (stockIm != null) stockIm.Qty -= delivery.Qty;

            // Финансовые эффекты
            item.DeliveredQty += delivery.Qty;
            if (item.IsDelivered) contract.DeliveredItemsCount++;
            contract.ShippedAmount += delivery.Qty * (delivery.UnitPrice > 0 ? delivery.UnitPrice : item.UnitPrice);

            // Логи/транзакции
            foreach (var b in delivery.Batches)
            {
                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductId = delivery.ProductId,
                    Register = b.RegisterAtDelivery,
                    Type = InventoryTransactionType.ContractDelivery,
                    Qty = -b.Qty,
                    UnitCost = b.UnitCost,
                    BatchId = b.BatchId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userName,
                    Note = $"Отгрузка (retry) по договору #{contractId}, доставка #{delivery.Id}"
                });
            }

            delivery.Status = ShipmentStatus.Completed;
            delivery.MissingQtyForConversion = null;

            UpdateContractStatus(contract);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return delivery;
        }
        catch (Exception ex)
        {
            delivery.RetryCount += 1;
            delivery.LastRetryAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            _logger.LogError(ex, "[ContractsService] Retry conversion failed for delivery #{DeliveryId}", deliveryId);
            return delivery;
        }
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

        // Автозакрытие для открытых договоров по достижении лимита (ShippedAmount >= LimitTotalUzs ?? TotalAmount)
        var limit = contract.LimitTotalUzs ?? contract.TotalAmount;
        if (contract.Type == ContractType.Open && limit > 0 && contract.ShippedAmount >= limit)
        {
            contract.Status = ContractStatus.Closed;
            return;
        }

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

    /// <summary>
    /// Конвертировать недостающее количество товара из ND-40 в IM-40 для конкретного товара.
    /// Перемещает партии FIFO, создавая новые партии IM-40 с той же себестоимостью.
    /// Возвращает фактически перемещённое количество.
    /// </summary>
    private async Task<decimal> ConvertNdToImAsync(int productId, decimal requiredQty, CancellationToken ct)
    {
        if (requiredQty <= 0) return 0m;

        var ndBatches = await _db.Batches
            .Where(b => b.ProductId == productId && b.Register == StockRegister.ND40 && b.Qty > 0)
            .OrderBy(b => b.CreatedAt).ThenBy(b => b.Id)
            .ToListAsync(ct);

        decimal moved = 0m;
        foreach (var nd in ndBatches)
        {
            if (moved >= requiredQty) break;
            var toMove = Math.Min(nd.Qty, requiredQty - moved);
            if (toMove <= 0) continue;

            // Уменьшаем ND-40 партию
            nd.Qty -= toMove;

            // Создаём IM-40 партию с той же себестоимостью
            var im = new Batch
            {
                ProductId = productId,
                Register = StockRegister.IM40,
                Qty = toMove,
                UnitCost = nd.UnitCost,
                CreatedAt = DateTime.UtcNow,
                Code = nd.Code,
                Note = $"Transfer ND→IM, batch #{nd.Id}",
                SupplierName = nd.SupplierName,
                InvoiceNumber = nd.InvoiceNumber,
                PurchaseDate = nd.PurchaseDate,
                VatRate = nd.VatRate,
                PurchaseSource = "transfer_nd_to_im:contract",
                GtdCode = nd.GtdCode
            };
            _db.Batches.Add(im);

            // Обновляем остатки на регистрах
            var ndStock = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == productId && s.Register == StockRegister.ND40, ct);
            if (ndStock != null) ndStock.Qty -= toMove;
            var imStock = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == productId && s.Register == StockRegister.IM40, ct);
            if (imStock == null)
            {
                imStock = new Stock { ProductId = productId, Register = StockRegister.IM40, Qty = 0 };
                _db.Stocks.Add(imStock);
            }
            imStock.Qty += toMove;

            // Пишем транзакции движения
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                ProductId = productId,
                Register = StockRegister.ND40,
                Type = InventoryTransactionType.MoveNdToIm,
                Qty = -toMove,
                UnitCost = nd.UnitCost,
                BatchId = nd.Id,
                CreatedAt = DateTime.UtcNow,
                Note = "Transfer ND→IM (contract delivery)"
            });
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                ProductId = productId,
                Register = StockRegister.IM40,
                Type = InventoryTransactionType.MoveNdToIm,
                Qty = toMove,
                UnitCost = nd.UnitCost,
                BatchId = null,
                CreatedAt = DateTime.UtcNow,
                Note = "Transfer ND→IM (contract delivery)"
            });

            moved += toMove;
        }

        return moved;
    }
}
