using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DebtsController(AppDbContext db) : ControllerBase
{
    // GET /api/debts?clientId=&status=&from=&to=&page=&size=
    [HttpGet]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> List([FromQuery] int? clientId, [FromQuery] DebtStatus? status,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page = 1, [FromQuery] int size = 50)
    {
        var q = db.Debts.AsNoTracking().AsQueryable();
        if (clientId.HasValue) q = q.Where(d => d.ClientId == clientId.Value);
        if (status.HasValue) q = q.Where(d => d.Status == status.Value);
        if (from.HasValue) q = q.Where(d => d.DueDate >= from.Value);
        if (to.HasValue) q = q.Where(d => d.DueDate < to.Value);
        page = Math.Max(1, page); size = Math.Clamp(size, 1, 200);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(d => d.DueDate).ThenBy(d => d.Id).Skip((page-1)*size).Take(size).ToListAsync();
        return Ok(new { items, total, page, size });
    }

    // GET /api/debts/{id}/payments
    [HttpGet("{id:int}/payments")]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> GetPayments(int id)
    {
        var debt = await db.Debts.FindAsync(id);
        if (debt is null) return NotFound();
        var pays = await db.DebtPayments.AsNoTracking().Where(p => p.DebtId == id).OrderBy(p => p.PaidAt).ToListAsync();
        return Ok(pays);
    }

    // POST /api/debts/{id}/pay
    [HttpPost("{id:int}/pay")]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> Pay(int id, [FromBody] DebtPayDto dto, CancellationToken ct)
    {
        if (dto.Amount <= 0) return ValidationProblem("Amount must be > 0");
        var debt = await db.Debts.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (debt is null) return NotFound();
        if (debt.Status == DebtStatus.Paid) return ValidationProblem("Debt already paid");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var pay = new DebtPayment { DebtId = id, Amount = dto.Amount, PaidAt = DateTime.UtcNow };
        db.DebtPayments.Add(pay);
        debt.Amount -= dto.Amount;
        if (debt.Amount <= 0)
        {
            debt.Amount = 0;
            debt.Status = DebtStatus.Paid;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new { debt.Id, debt.Amount, debt.Status });
    }
}
