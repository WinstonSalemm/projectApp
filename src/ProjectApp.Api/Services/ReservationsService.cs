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
                Register = StockRegister.ND40, // По умолчанию будем пытаться взять с ND-40
                Qty = it.Qty,
                Sku = p.Sku,
                Name = p.Name,
                UnitPrice = p.Price
            };
            res.Items.Add(resItem);

            // Списываем товар: сначала с ND-40, потом с IM-40
            var consumptions = new List<(int batchId, StockRegister register, decimal qty, decimal unitCost)>();
            
            // 1) Пытаемся взять с ND-40
            var stockNd = await _db.Stocks.FirstOrDefaultAsync(
                s => s.ProductId == p.Id && s.Register == StockRegister.ND40, ct);
            var availableNd = stockNd?.Qty ?? 0m;
            var takeNd = Math.Min(availableNd, it.Qty);

            if (takeNd > 0)
            {
                var (batches, _) = await DeductFromBatchesAsync(p.Id, StockRegister.ND40, takeNd, ct);
                foreach (var b in batches)
                {
                    consumptions.Add((b.batchId, StockRegister.ND40, b.qty, b.unitCost));
                }
                stockNd!.Qty -= takeNd;
            }

            // 2) Остаток берем с IM-40
            var remain = it.Qty - takeNd;
            if (remain > 0)
            {
                var stockIm = await _db.Stocks.FirstOrDefaultAsync(
                    s => s.ProductId == p.Id && s.Register == StockRegister.IM40, ct);
                
                if (stockIm == null || stockIm.Qty < remain)
                {
                    var available = stockIm?.Qty ?? 0m;
                    throw new InvalidOperationException(
                        $"Недостаточно товара '{p.Name}' на складе. Требуется {remain}, доступно {available} в IM-40");
                }

                var (batches, _) = await DeductFromBatchesAsync(p.Id, StockRegister.IM40, remain, ct);
                foreach (var b in batches)
                {
                    consumptions.Add((b.batchId, StockRegister.IM40, b.qty, b.unitCost));
                }
                stockIm.Qty -= remain;
            }

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
