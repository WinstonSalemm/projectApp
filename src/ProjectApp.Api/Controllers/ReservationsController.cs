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

