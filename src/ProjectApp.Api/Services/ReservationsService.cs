using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectApp.Api.Data;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Integrations.Telegram;
using ProjectApp.Api.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ProjectApp.Api.Services;

public class ReservationsService
{
    private readonly AppDbContext _db;
    private readonly ReservationsOptions _opts;
    private readonly ITelegramService _tg;
    private readonly TelegramSettings _tgSettings;
    private readonly ILogger<ReservationsService> _logger;

    public ReservationsService(AppDbContext db, IOptions<ReservationsOptions> opts, ITelegramService tg, IOptions<TelegramSettings> tgs, ILogger<ReservationsService> logger)
    {
        _db = db;
        _opts = opts.Value;
        _tg = tg;
        _tgSettings = tgs.Value;
        _logger = logger;
    }

    public async Task<Reservation> CreateAsync(ReservationCreateDto dto, string createdBy, CancellationToken ct = default)
    {
        if (dto.Items == null || dto.Items.Count == 0) throw new InvalidOperationException("Items are required");

        var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToArray();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        if (products.Count != productIds.Length) throw new InvalidOperationException("Some products not found");

        var days = dto.Paid ? _opts.PaidDays : _opts.UnpaidDays;
        var now = DateTime.UtcNow;
        
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        
        var res = new Reservation
        {
            ClientId = dto.ClientId,
            CreatedBy = createdBy,
            CreatedAt = now,
            Paid = dto.Paid,
            ReservedUntil = now.AddDays(days),
            Status = ReservationStatus.Active,
            Note = string.IsNullOrWhiteSpace(dto.Source) ? dto.Note : $"{dto.Note} (src:{dto.Source})"
        };

        // Будем собирать информацию о списании по партиям
        var batchConsumptions = new Dictionary<ReservationItem, List<(int batchId, StockRegister register, decimal qty, decimal unitCost)>>();

        foreach (var it in dto.Items)
        {
            if (!products.TryGetValue(it.ProductId, out var p)) 
                throw new InvalidOperationException($"Product not found: {it.ProductId}");
            if (it.Qty <= 0) 
                throw new InvalidOperationException("Qty must be > 0");

            var resItem = new ReservationItem
            {
                ProductId = p.Id,
                Register = StockRegister.IM40, // По умолчанию считаем IM-40, скорректируем ниже при отсутствии IM-40
                Qty = it.Qty,
                Sku = p.Sku,
                Name = p.Name,
                UnitPrice = p.Price
            };
            res.Items.Add(resItem);

            // Списываем товар: сначала с IM-40, потом с ND-40
            var consumptions = new List<(int batchId, StockRegister register, decimal qty, decimal unitCost)>();
            
            // 1) Пытаемся взять с IM-40
            var stockIm = await _db.Stocks.FirstOrDefaultAsync(
                s => s.ProductId == p.Id && s.Register == StockRegister.IM40, ct);
            var availableIm = stockIm?.Qty ?? 0m;
            var takeIm = Math.Min(availableIm, it.Qty);

            if (takeIm > 0)
            {
                var (batches, _) = await DeductFromBatchesAsync(p.Id, StockRegister.IM40, takeIm, ct);
                foreach (var b in batches)
                {
                    consumptions.Add((b.batchId, StockRegister.IM40, b.qty, b.unitCost));
                }
                stockIm!.Qty -= takeIm;
            }

            // 2) Остаток берем с ND-40
            var remain = it.Qty - takeIm;
            if (remain > 0)
            {
                var stockNd = await _db.Stocks.FirstOrDefaultAsync(
                    s => s.ProductId == p.Id && s.Register == StockRegister.ND40, ct);

                if (stockNd == null || stockNd.Qty < remain)
                {
                    var available = stockNd?.Qty ?? 0m;
                    throw new InvalidOperationException(
                        $"Недостаточно товара '{p.Name}' на складе. Требуется {remain}, доступно {available} в ND-40");
                }

                var (batches, _) = await DeductFromBatchesAsync(p.Id, StockRegister.ND40, remain, ct);
                foreach (var b in batches)
                {
                    consumptions.Add((b.batchId, StockRegister.ND40, b.qty, b.unitCost));
                }
                stockNd.Qty -= remain;
            }

            // Отразим основной регистр позиции: если есть любая часть из IM-40 — IM-40, иначе ND-40
            resItem.Register = takeIm > 0 ? StockRegister.IM40 : StockRegister.ND40;

            batchConsumptions[resItem] = consumptions;
        }

        _db.Reservations.Add(res);
        await _db.SaveChangesAsync(ct); // Сохраняем чтобы получить ID

        // Сохраняем связи с партиями
        foreach (var item in res.Items)
        {
            if (!batchConsumptions.TryGetValue(item, out var list)) continue;
            
            foreach (var (batchId, register, qty, unitCost) in list)
            {
                _db.ReservationItemBatches.Add(new ReservationItemBatch
                {
                    ReservationItemId = item.Id,
                    BatchId = batchId,
                    RegisterAtReservation = register,
                    Qty = qty,
                    UnitCost = unitCost
                });

                // Логируем транзакцию
                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductId = item.ProductId,
                    Register = register,
                    Type = InventoryTransactionType.Reservation,
                    Qty = -qty,
                    UnitCost = unitCost,
                    BatchId = batchId,
                    ReservationId = res.Id,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = createdBy,
                    Note = $"reservation #{res.Id}"
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await LogAsync(res.Id, "Created", createdBy, $"Items:{res.Items.Count}; Paid:{res.Paid}; Until:{res.ReservedUntil:o}", ct);

        return res;
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

    public async Task<bool> AddPhotoAndNotifyAsync(int reservationId, Stream fileStream, string fileName, string userName, CancellationToken ct = default)
    {
        var res = await _db.Reservations.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == reservationId, ct);
        if (res == null) return false;
        if (res.Status != ReservationStatus.Active) return false;

        // Read, resize, recompress to JPEG under limits
        using var image = await Image.LoadAsync(fileStream, ct);
        var longSide = Math.Max(image.Width, image.Height);
        if (longSide > _opts.Photo.MaxLongSide)
        {
            var scale = (double)_opts.Photo.MaxLongSide / longSide;
            var newW = (int)Math.Round(image.Width * scale);
            var newH = (int)Math.Round(image.Height * scale);
            image.Mutate(x => x.Resize(newW, newH));
        }
        var baseDir = Path.Combine(AppContext.BaseDirectory, "reservation-photos");
        Directory.CreateDirectory(baseDir);
        var savePath = Path.Combine(baseDir, $"reservation_{reservationId}_{DateTime.UtcNow:yyyyMMddHHmmss}.jpg");
        await using (var outStream = File.Create(savePath))
        {
            var enc = new JpegEncoder { Quality = _opts.Photo.JpegQuality };
            await image.SaveAsJpegAsync(outStream, enc, ct);
        }
        var fi = new FileInfo(savePath);
        if (fi.Length > _opts.Photo.MaxBytes)
        {
            // If still too big, try re-encode lower quality
            await using (var outStream = File.Create(savePath))
            {
                var enc = new JpegEncoder { Quality = Math.Max(50, _opts.Photo.JpegQuality - 15) };
                await image.SaveAsJpegAsync(outStream, enc, ct);
            }
            fi.Refresh();
        }

        res.PhotoPath = savePath;
        res.PhotoMime = "image/jpeg";
        res.PhotoSize = fi.Length;
        res.PhotoCreatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await LogAsync(res.Id, "PhotoAttached", userName, null, ct);
        await NotifyWithPhotoAsync(res, ct);
        return true;
    }

    public async Task<bool> FulfillAsync(int reservationId, string userName, CancellationToken ct = default)
    {
        var res = await _db.Reservations
            .FirstOrDefaultAsync(r => r.Id == reservationId, ct);
        if (res == null) return false;
        if (res.Status != ReservationStatus.Active) return false;

        // Меняем только статус без движения по складу (списание уже было при создании)
        res.Status = ReservationStatus.Fulfilled;
        res.ReservedUntil = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await LogAsync(res.Id, "Fulfilled", userName, null, ct);

        // Уведомление текстом
        _ = NotifyTextOnlyAsync(res.Id, CancellationToken.None);
        return true;
    }

    public class ReservationUpdateItemDto
    {
        public int ProductId { get; set; }
        public decimal Qty { get; set; }
    }

    public async Task<bool> UpdateAsync(int reservationId, List<ReservationUpdateItemDto> items, string userName, CancellationToken ct = default)
    {
        var res = await _db.Reservations
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == reservationId, ct);
        if (res == null) return false;
        if (res.Status != ReservationStatus.Active) return false;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Map current by product
        var currentByProduct = res.Items.ToDictionary(i => i.ProductId, i => i);
        var desiredByProduct = items.GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

        // Helper to recalc Register flag per item
        async Task RecalcRegisterAsync(ReservationItem it, CancellationToken ctoken)
        {
            var anyIm = await _db.ReservationItemBatches.AsNoTracking()
                .AnyAsync(b => b.ReservationItemId == it.Id && b.RegisterAtReservation == StockRegister.IM40, ctoken);
            it.Register = anyIm ? StockRegister.IM40 : StockRegister.ND40;
        }

        // Remove items not present anymore or reduce qty
        foreach (var cur in res.Items.ToList())
        {
            desiredByProduct.TryGetValue(cur.ProductId, out var newQty);
            var oldQty = cur.Qty;

            if (!desiredByProduct.ContainsKey(cur.ProductId) || newQty <= 0)
            {
                // Return all batches
                var batches = await _db.ReservationItemBatches.Where(b => b.ReservationItemId == cur.Id).ToListAsync(ct);
                foreach (var itemBatch in batches)
                {
                    var batch = await _db.Batches.FirstOrDefaultAsync(b => b.Id == itemBatch.BatchId, ct);
                    if (batch != null) batch.Qty += itemBatch.Qty;
                    var stock = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == cur.ProductId && s.Register == itemBatch.RegisterAtReservation, ct);
                    if (stock != null) stock.Qty += itemBatch.Qty; else _db.Stocks.Add(new Stock { ProductId = cur.ProductId, Register = itemBatch.RegisterAtReservation, Qty = itemBatch.Qty });
                    _db.InventoryTransactions.Add(new InventoryTransaction
                    {
                        ProductId = cur.ProductId,
                        Register = itemBatch.RegisterAtReservation,
                        Type = InventoryTransactionType.ReservationCancelled,
                        Qty = itemBatch.Qty,
                        UnitCost = itemBatch.UnitCost,
                        BatchId = itemBatch.BatchId,
                        ReservationId = res.Id,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = userName,
                        Note = $"reservation #{res.Id} edit remove"
                    });
                }
                _db.ReservationItemBatches.RemoveRange(batches);
                _db.ReservationItems.Remove(cur);
            }
            else if (newQty < oldQty)
            {
                // Return part of qty from batches FIFO (oldest first recorded)
                var needReturn = oldQty - newQty;
                var batches = await _db.ReservationItemBatches.Where(b => b.ReservationItemId == cur.Id).OrderBy(b => b.Id).ToListAsync(ct);
                foreach (var ib in batches)
                {
                    if (needReturn <= 0) break;
                    var take = Math.Min(ib.Qty, needReturn);
                    if (take > 0)
                    {
                        ib.Qty -= take;
                        needReturn -= take;
                        var batch = await _db.Batches.FirstOrDefaultAsync(b => b.Id == ib.BatchId, ct);
                        if (batch != null) batch.Qty += take;
                        var stock = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == cur.ProductId && s.Register == ib.RegisterAtReservation, ct);
                        if (stock != null) stock.Qty += take; else _db.Stocks.Add(new Stock { ProductId = cur.ProductId, Register = ib.RegisterAtReservation, Qty = take });
                        _db.InventoryTransactions.Add(new InventoryTransaction
                        {
                            ProductId = cur.ProductId,
                            Register = ib.RegisterAtReservation,
                            Type = InventoryTransactionType.ReservationCancelled,
                            Qty = take,
                            UnitCost = ib.UnitCost,
                            BatchId = ib.BatchId,
                            ReservationId = res.Id,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = userName,
                            Note = $"reservation #{res.Id} edit reduce"
                        });
                    }
                }
                // Remove zero qty links
                var zeros = batches.Where(b => b.Qty <= 0).ToList();
                if (zeros.Count > 0) _db.ReservationItemBatches.RemoveRange(zeros);
                cur.Qty = newQty;
                await RecalcRegisterAsync(cur, ct);
            }
        }

        // Add or increase items
        var productIds = desiredByProduct.Keys.ToArray();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        foreach (var kv in desiredByProduct)
        {
            var pid = kv.Key; var wantQty = kv.Value;
            currentByProduct.TryGetValue(pid, out var curItem);
            var curQty = curItem?.Qty ?? 0m;
            if (wantQty > curQty)
            {
                var add = wantQty - curQty;
                if (!products.TryGetValue(pid, out var p)) throw new InvalidOperationException($"Product not found: {pid}");
                if (curItem == null)
                {
                    curItem = new ReservationItem { ProductId = p.Id, Qty = 0, Register = StockRegister.IM40, Sku = p.Sku, Name = p.Name, UnitPrice = p.Price };
                    res.Items.Add(curItem);
                    await _db.SaveChangesAsync(ct); // get Id for batches
                }
                // Deduct IM-40 then ND-40
                var consumptions = new List<(int batchId, StockRegister register, decimal qty, decimal unitCost)>();
                var stockIm = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == p.Id && s.Register == StockRegister.IM40, ct);
                var takeIm = Math.Min(stockIm?.Qty ?? 0m, add);
                if (takeIm > 0)
                {
                    var (batches, _) = await DeductFromBatchesAsync(p.Id, StockRegister.IM40, takeIm, ct);
                    foreach (var b in batches) consumptions.Add((b.batchId, StockRegister.IM40, b.qty, b.unitCost));
                    if (stockIm != null) stockIm.Qty -= takeIm;
                }
                var remain = add - takeIm;
                if (remain > 0)
                {
                    var stockNd = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == p.Id && s.Register == StockRegister.ND40, ct);
                    if (stockNd == null || stockNd.Qty < remain)
                    {
                        var available = stockNd?.Qty ?? 0m;
                        throw new InvalidOperationException($"Недостаточно товара '{p.Name}' на складе. Требуется {remain}, доступно {available} в ND-40");
                    }
                    var (batches, _) = await DeductFromBatchesAsync(p.Id, StockRegister.ND40, remain, ct);
                    foreach (var b in batches) consumptions.Add((b.batchId, StockRegister.ND40, b.qty, b.unitCost));
                    stockNd.Qty -= remain;
                }
                foreach (var (batchId, reg, qty, unitCost) in consumptions)
                {
                    _db.ReservationItemBatches.Add(new ReservationItemBatch
                    {
                        ReservationItemId = curItem.Id,
                        BatchId = batchId,
                        RegisterAtReservation = reg,
                        Qty = qty,
                        UnitCost = unitCost
                    });
                    _db.InventoryTransactions.Add(new InventoryTransaction
                    {
                        ProductId = curItem.ProductId,
                        Register = reg,
                        Type = InventoryTransactionType.Reservation,
                        Qty = -qty,
                        UnitCost = unitCost,
                        BatchId = batchId,
                        ReservationId = res.Id,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = userName,
                        Note = $"reservation #{res.Id} edit add"
                    });
                }
                curItem.Qty += add;
                await RecalcRegisterAsync(curItem, ct);
            }
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await LogAsync(res.Id, "Edited", userName, null, ct);
        _ = NotifyTextOnlyAsync(res.Id, CancellationToken.None);
        return true;
    }

    public async Task<bool> NotifyTextOnlyAsync(int reservationId, CancellationToken ct = default)
    {
        var res = await _db.Reservations.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == reservationId, ct);
        if (res == null) return false;
        var caption = await BuildCaptionHtmlAsync(res, ct);
        var ids = _tgSettings.ParseAllowedChatIds();
        foreach (var id in ids)
        {
            try { await _tg.SendMessageAsync(id, caption, parseMode: "HTML", replyMarkup: null, ct); } catch { }
        }
        return true;
    }

    public async Task<bool> ExtendAsync(int reservationId, bool? paid, int? days, string userName, CancellationToken ct = default)
    {
        var res = await _db.Reservations.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == reservationId, ct);
        if (res == null) return false;
        if (res.Status != ReservationStatus.Active) return false;

        if (paid.HasValue) res.Paid = paid.Value;
        var effDays = days ?? (res.Paid ? _opts.PaidDays : _opts.UnpaidDays);
        res.ReservedUntil = DateTime.UtcNow.AddDays(effDays);
        await _db.SaveChangesAsync(ct);
        await LogAsync(res.Id, "Extended", userName, $"Paid:{res.Paid}; Until:{res.ReservedUntil:o}", ct);

        // Notify text about updated reservation
        _ = NotifyTextOnlyAsync(res.Id, CancellationToken.None);
        return true;
    }

    public async Task<bool> ReleaseAsync(int reservationId, string? reason, string userName, CancellationToken ct = default)
    {
        var res = await _db.Reservations
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == reservationId, ct);
        
        if (res == null) return false;
        if (res.Status != ReservationStatus.Active) return false;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Возвращаем товар в те же партии, откуда брали
        foreach (var item in res.Items)
        {
            var batches = await _db.ReservationItemBatches
                .Where(b => b.ReservationItemId == item.Id)
                .ToListAsync(ct);

            foreach (var itemBatch in batches)
            {
                // Находим партию
                var batch = await _db.Batches.FirstOrDefaultAsync(b => b.Id == itemBatch.BatchId, ct);
                if (batch == null) continue; // Партия удалена? Пропускаем

                // Возвращаем товар в партию
                batch.Qty += itemBatch.Qty;

                // Обновляем остатки на складе
                var stock = await _db.Stocks.FirstOrDefaultAsync(
                    s => s.ProductId == item.ProductId && s.Register == itemBatch.RegisterAtReservation, ct);
                
                if (stock != null)
                {
                    stock.Qty += itemBatch.Qty;
                }
                else
                {
                    // Создаем запись если её нет
                    _db.Stocks.Add(new Stock
                    {
                        ProductId = item.ProductId,
                        Register = itemBatch.RegisterAtReservation,
                        Qty = itemBatch.Qty
                    });
                }

                // Логируем транзакцию возврата
                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductId = item.ProductId,
                    Register = itemBatch.RegisterAtReservation,
                    Type = InventoryTransactionType.ReservationCancelled,
                    Qty = itemBatch.Qty,
                    UnitCost = itemBatch.UnitCost,
                    BatchId = batch.Id,
                    ReservationId = res.Id,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userName,
                    Note = $"reservation #{res.Id} cancelled"
                });
            }
        }

        res.Status = ReservationStatus.Released;
        res.ReservedUntil = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        
        await LogAsync(res.Id, "Released", userName, reason, ct);

        // Notify text about release
        _ = NotifyTextOnlyAsync(res.Id, CancellationToken.None);
        return true;
    }

    private async Task NotifyWithPhotoAsync(Reservation res, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(res.PhotoPath) || !File.Exists(res.PhotoPath))
        {
            await NotifyTextOnlyAsync(res.Id, ct);
            return;
        }
        var caption = await BuildCaptionHtmlAsync(res, ct);
        var ids = _tgSettings.ParseAllowedChatIds();
        await using var stream = File.OpenRead(res.PhotoPath);
        foreach (var chatId in ids)
        {
            stream.Position = 0;
            var ok = await _tg.SendPhotoAsync(chatId, stream, Path.GetFileName(res.PhotoPath), caption, "HTML", ct);
            if (!ok) _logger.LogWarning("Failed to send reservation photo to chat {ChatId} for res {Id}", chatId, res.Id);
        }
    }

    private async Task<string> BuildCaptionHtmlAsync(Reservation res, CancellationToken ct)
    {
        string Html(string? s) => string.IsNullOrEmpty(s) ? string.Empty : s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        var clientName = "";
        if (res.ClientId.HasValue)
        {
            var c = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == res.ClientId.Value, ct);
            if (c != null) clientName = Html(c.Name);
        }
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>РЕЗЕРВ</b> {(res.Paid ? "<i>(оплачен)</i>" : "<i>(без оплаты)</i>")} до {res.ReservedUntil:yyyy-MM-dd HH:mm}");
        if (!string.IsNullOrEmpty(clientName)) sb.AppendLine($"Клиент: <b>{clientName}</b>");
        sb.AppendLine($"Менеджер: <b>{Html(res.CreatedBy)}</b>");
        sb.AppendLine("\n<b>Позиции:</b>");
        decimal total = 0m;
        foreach (var it in res.Items)
        {
            var row = it.UnitPrice * (decimal)it.Qty;
            total += row;
            var reg = it.Register == StockRegister.IM40 ? "IM-40" : "ND-40";
            sb.AppendLine($"• {Html(it.Sku)} | {Html(it.Name)} | {reg} | {it.Qty:0.###} × {it.UnitPrice:0.##} = <b>{row:0.##}</b>");
        }
        sb.AppendLine($"\nИтого: <b>{total:0.##}</b>");
        return sb.ToString();
    }

    private async Task LogAsync(int resId, string action, string user, string? details, CancellationToken ct)
    {
        _db.ReservationLogs.Add(new ReservationLog
        {
            ReservationId = resId,
            Action = action,
            UserName = user,
            Details = details,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }
}
