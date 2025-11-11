using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Models;
using ProjectApp.Api.Services;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReservationsController : ControllerBase
{
    private readonly ReservationsService _svc;
    private readonly AppDbContext _db;
    private readonly ILogger<ReservationsController> _logger;

    public ReservationsController(ReservationsService svc, AppDbContext db, ILogger<ReservationsController> logger)
    {
        _svc = svc;
        _db = db;
        _logger = logger;
    }

    [HttpPost]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(ReservationViewDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] ReservationCreateDto dto, CancellationToken ct)
    {
        try
        {
            if (!dto.ClientId.HasValue || dto.ClientId.Value <= 0)
                return ValidationProblem(detail: "ClientId is required for reservation");
            var createdBy = User?.Identity?.Name ?? "unknown";
            var res = await _svc.CreateAsync(dto, createdBy, ct);

            // If not waiting for photo, send text-only notify immediately (Windows path)
            if (!(dto.WaitForPhoto ?? false))
            {
                try { await _svc.NotifyTextOnlyAsync(res.Id, ct); } catch { }
            }

            var view = new ReservationViewDto
            {
                Id = res.Id,
                ClientId = res.ClientId,
                Paid = res.Paid,
                ReservedUntil = res.ReservedUntil,
                Status = res.Status,
                Items = res.Items.Select(i => new ReservationItemViewDto
                {
                    ProductId = i.ProductId,
                    Sku = i.Sku,
                    Name = i.Name,
                    Register = i.Register,
                    Qty = i.Qty,
                    UnitPrice = i.UnitPrice
                }).ToList()
            };
            return Created($"/api/reservations/{res.Id}", view);
        }
        catch (InvalidOperationException ex)
        {
            return ValidationProblem(detail: ex.Message);
        }
        catch (DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return ValidationProblem(detail: msg);
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            _logger.LogError(ex, "Error creating reservation");
            return ValidationProblem(detail: msg);
        }
    }

    public class ReservationItemsUpdateDto
    {
        public List<ItemDto> Items { get; set; } = new();
        public class ItemDto { public int ProductId { get; set; } public decimal Qty { get; set; } }
    }

    [HttpPatch("{id:int}/items")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateItems([FromRoute] int id, [FromBody] ReservationItemsUpdateDto dto, CancellationToken ct)
    {
        if (dto == null || dto.Items == null) return ValidationProblem(detail: "Items are required");
        var user = User?.Identity?.Name ?? "unknown";
        try
        {
            var ok = await _svc.UpdateAsync(id, dto.Items.Select(x => new ReservationsService.ReservationUpdateItemDto { ProductId = x.ProductId, Qty = x.Qty }).ToList(), user, ct);
            if (!ok) return ValidationProblem(detail: $"Reservation not found or inactive: {id}");
            return NoContent();
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            _logger.LogError(ex, "Error updating reservation {Id}", id);
            return ValidationProblem(detail: msg);
        }
    }

    [HttpPatch("{id:int}/pay")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Pay([FromRoute] int id, [FromBody] ReservationPayDto dto, CancellationToken ct)
    {
        if (dto is null || dto.Amount <= 0) return ValidationProblem(detail: "Amount must be > 0");
        var res = await _db.Reservations.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (res is null) return ValidationProblem(detail: $"Reservation not found: {id}");
        if (res.Status != ReservationStatus.Active) return ValidationProblem(detail: "Reservation is not active");

        var user = User?.Identity?.Name ?? "unknown";
        _db.ReservationPayments.Add(new ReservationPayment
        {
            ReservationId = id,
            Amount = dto.Amount,
            Method = dto.Method,
            Note = dto.Note,
            PaidAt = DateTime.UtcNow,
            ReceivedBy = user
        });
        await _db.SaveChangesAsync(ct);

        // Recalculate paid state
        var paidAmount = await _db.ReservationPayments.Where(p => p.ReservationId == id).SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var total = res.Items.Sum(i => i.Qty * i.UnitPrice);
        if (paidAmount >= total && !res.Paid)
        {
            res.Paid = true;
            await _db.SaveChangesAsync(ct);
        }

        // Log payment
        _db.ReservationLogs.Add(new ReservationLog
        {
            ReservationId = res.Id,
            Action = "Payment",
            UserName = user,
            CreatedAt = DateTime.UtcNow,
            Details = $"Amount:{dto.Amount}; Method:{dto.Method}; Note:{dto.Note}"
        });
        await _db.SaveChangesAsync(ct);

        try { await _svc.NotifyTextOnlyAsync(res.Id, ct); } catch { }
        return NoContent();
    }

    [HttpGet]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int? clientId, [FromQuery] bool? mine, [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo, CancellationToken ct)
    {
        var q = _db.Reservations.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReservationStatus>(status, true, out var st))
            q = q.Where(r => r.Status == st);
        if (clientId.HasValue) q = q.Where(r => r.ClientId == clientId.Value);
        if (mine == true)
        {
            var user = User?.Identity?.Name ?? "unknown";
            q = q.Where(r => r.CreatedBy == user);
        }
        if (dateFrom.HasValue) q = q.Where(r => r.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue) q = q.Where(r => r.CreatedAt < dateTo.Value);
        var raw = await q
            .Select(r => new
            {
                r.Id,
                r.ClientId,
                ClientName = _db.Clients.Where(c => c.Id == r.ClientId).Select(c => c.Name).FirstOrDefault(),
                ClientPhone = _db.Clients.Where(c => c.Id == r.ClientId).Select(c => c.Phone).FirstOrDefault(),
                r.CreatedBy,
                r.CreatedAt,
                r.ReservedUntil,
                r.Status,
                r.Paid,
                ItemsCount = _db.ReservationItems.Count(i => i.ReservationId == r.Id),
                Total = _db.ReservationItems.Where(i => i.ReservationId == r.Id).Sum(i => (decimal?)i.Qty * i.UnitPrice) ?? 0m,
                PaidAmount = _db.ReservationPayments.Where(p => p.ReservationId == r.Id).Sum(p => (decimal?)p.Amount) ?? 0m,
                r.Note
            })
            .ToListAsync(ct);
        // Незакрытые (Active/Expired) сверху, затем остальные, далее по убыванию Id
        var list = raw
            .OrderBy(x => (x.Status == ReservationStatus.Active || x.Status == ReservationStatus.Expired) ? 0 : 1)
            .ThenByDescending(x => x.Id)
            .ToList();
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Details([FromRoute] int id, CancellationToken ct)
    {
        var r = await _db.Reservations.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return NotFound();
        var client = r.ClientId.HasValue ? await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == r.ClientId.Value, ct) : null;
        var payments = await _db.ReservationPayments.AsNoTracking().Where(p => p.ReservationId == id).OrderBy(p => p.PaidAt).ToListAsync(ct);
        var total = r.Items.Sum(i => i.Qty * i.UnitPrice);
        var paidAmount = payments.Sum(p => p.Amount);
        var due = Math.Max(0m, total - paidAmount);
        var result = new
        {
            r.Id,
            r.ClientId,
            ClientName = client?.Name,
            ClientPhone = client?.Phone,
            r.CreatedBy,
            r.CreatedAt,
            r.ReservedUntil,
            r.Status,
            r.Paid,
            r.Note,
            Total = total,
            PaidAmount = paidAmount,
            DueAmount = due,
            Items = r.Items.Select(i => new { i.ProductId, i.Sku, i.Name, i.Register, i.Qty, i.UnitPrice }).ToList(),
            Payments = payments.Select(p => new { p.Id, p.Amount, p.Method, p.Note, p.PaidAt, p.ReceivedBy }).ToList()
        };
        return Ok(result);
    }

    [HttpPost("{id:int}/photo")]
    [Authorize(Policy = "ManagerOnly")]
    [RequestSizeLimit(20_000_000)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadPhoto([FromRoute] int id, CancellationToken ct)
    {
        if (!Request.HasFormContentType) return ValidationProblem(detail: "Expected multipart/form-data");
        var form = await Request.ReadFormAsync(ct);
        var file = form.Files["file"] ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0) return ValidationProblem(detail: "Photo file is required");

        var user = User?.Identity?.Name ?? "unknown";
        await using var stream = file.OpenReadStream();
        var ok = await _svc.AddPhotoAndNotifyAsync(id, stream, file.FileName ?? "photo.jpg", user, ct);
        if (!ok) return ValidationProblem(detail: $"Reservation not found or inactive: {id}");
        return NoContent();
    }

    [HttpPatch("{id:int}/extend")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Extend([FromRoute] int id, [FromBody] ReservationExtendDto dto, CancellationToken ct)
    {
        var user = User?.Identity?.Name ?? "unknown";
        try
        {
            var ok = await _svc.ExtendAsync(id, dto.Paid, dto.Days, user, ct);
            if (!ok) return ValidationProblem(detail: $"Reservation not found or inactive: {id}");
            return NoContent();
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            _logger.LogError(ex, "Error extending reservation {Id}", id);
            return ValidationProblem(detail: msg);
        }
    }

    [HttpPost("{id:int}/release")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Release([FromRoute] int id, [FromBody] ReservationReleaseDto dto, CancellationToken ct)
    {
        var user = User?.Identity?.Name ?? "unknown";
        try
        {
            var ok = await _svc.ReleaseAsync(id, dto?.Reason, user, ct);
            if (!ok) return ValidationProblem(detail: $"Reservation not found or inactive: {id}");
            return NoContent();
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            _logger.LogError(ex, "Error releasing reservation {Id}", id);
            return ValidationProblem(detail: msg);
        }
    }

    [HttpPost("{id:int}/fulfill")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Fulfill([FromRoute] int id, CancellationToken ct)
    {
        var user = User?.Identity?.Name ?? "unknown";
        try
        {
            var ok = await _svc.FulfillAsync(id, user, ct);
            if (!ok) return ValidationProblem(detail: $"Reservation not found or inactive: {id}");
            return NoContent();
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            _logger.LogError(ex, "Error fulfilling reservation {Id}", id);
            return ValidationProblem(detail: msg);
        }
    }

    [HttpGet("alerts")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(IEnumerable<ReservationAlertDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAlerts([FromQuery] DateTime? since, CancellationToken ct)
    {
        var user = User?.Identity?.Name ?? "unknown";

        var activeQuery = from r in _db.Reservations.AsNoTracking()
                          where r.CreatedBy == user && r.Status == ReservationStatus.Active
                          join c in _db.Clients.AsNoTracking() on r.ClientId equals c.Id into gj
                          from c in gj.DefaultIfEmpty()
                          select new ReservationAlertDto
                          {
                              Id = r.Id,
                              ClientName = c != null ? c.Name : null,
                              ClientPhone = c != null ? c.Phone : null,
                              CreatedAt = r.CreatedAt,
                              ReservedUntil = r.ReservedUntil,
                              Paid = r.Paid,
                              Status = r.Status.ToString(),
                              SaleId = r.SaleId
                          };

        var active = await activeQuery.ToListAsync(ct);

        List<ReservationAlertDto> expired = new();
        if (since.HasValue)
        {
            var sinceUtc = DateTime.SpecifyKind(since.Value, DateTimeKind.Utc);
            expired = await (from log in _db.ReservationLogs.AsNoTracking()
                             where log.Action == "Expired" && log.CreatedAt >= sinceUtc
                             join r in _db.Reservations.AsNoTracking() on log.ReservationId equals r.Id
                             where r.CreatedBy == user
                             join c in _db.Clients.AsNoTracking() on r.ClientId equals c.Id into gj
                             from c in gj.DefaultIfEmpty()
                             select new ReservationAlertDto
                             {
                                 Id = r.Id,
                                 ClientName = c != null ? c.Name : null,
                                 ClientPhone = c != null ? c.Phone : null,
                                 CreatedAt = r.CreatedAt,
                                 ReservedUntil = r.ReservedUntil,
                                 Paid = r.Paid,
                                 Status = r.Status.ToString(),
                                 SaleId = r.SaleId
                             }).ToListAsync(ct);
        }

        var all = active.Concat(expired)
            .GroupBy(x => x.Id)
            .Select(g => g.First())
            .OrderBy(x => x.Id)
            .ToList();

        return Ok(all);
    }
}

