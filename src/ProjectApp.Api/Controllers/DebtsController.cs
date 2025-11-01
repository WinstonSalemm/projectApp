using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Models;
using ProjectApp.Api.Integrations.Telegram;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DebtsController(AppDbContext db, IDebtsNotifier debtsNotifier) : ControllerBase
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

    // GET /api/debts/{id}
    /// <summary>
    /// Получить детали долга с товарами
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> GetDetails(int id)
    {
        var debt = await db.Debts
            .Include(d => d.Items)
            .FirstOrDefaultAsync(d => d.Id == id);
        
        if (debt is null) return NotFound();

        var client = await db.Clients.FindAsync(debt.ClientId);
        var paidAmount = debt.OriginalAmount - debt.Amount;

        var result = new DebtDetailsDto
        {
            Id = debt.Id,
            ClientId = debt.ClientId,
            ClientName = client?.Name ?? "Неизвестный клиент",
            SaleId = debt.SaleId,
            Amount = debt.Amount,
            OriginalAmount = debt.OriginalAmount,
            PaidAmount = paidAmount,
            DueDate = debt.DueDate,
            Status = debt.Status.ToString(),
            Items = debt.Items.Select(i => new DebtItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Sku = i.Sku,
                Qty = i.Qty,
                Price = i.Price,
                Total = i.Total
            }).ToList(),
            Notes = debt.Notes,
            CreatedAt = debt.CreatedAt,
            CreatedBy = debt.CreatedBy
        };

        return Ok(result);
    }

    // GET /api/debts/by-client/{clientId}
    /// <summary>
    /// Получить все долги конкретного клиента
    /// </summary>
    [HttpGet("by-client/{clientId:int}")]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> GetByClient(int clientId)
    {
        var debts = await db.Debts
            .AsNoTracking()
            .Where(d => d.ClientId == clientId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        var totalDebt = debts.Where(d => d.Status == DebtStatus.Open).Sum(d => d.Amount);

        return Ok(new
        {
            clientId,
            totalDebt,
            debts
        });
    }

    // PUT /api/debts/{id}/items
    /// <summary>
    /// Редактировать товары в долге (изменить цену/количество)
    /// </summary>
    [HttpPut("{id:int}/items")]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> UpdateItems(int id, [FromBody] UpdateDebtItemsDto dto, CancellationToken ct)
    {
        var debt = await db.Debts
            .Include(d => d.Items)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        
        if (debt is null) return NotFound();
        if (debt.Status == DebtStatus.Paid) return ValidationProblem("Нельзя редактировать оплаченный долг");

        // Обновляем товары
        foreach (var itemDto in dto.Items)
        {
            var existingItem = debt.Items.FirstOrDefault(i => i.Id == itemDto.Id);
            if (existingItem != null)
            {
                existingItem.Qty = itemDto.Qty;
                existingItem.Price = itemDto.Price;
                existingItem.Total = itemDto.Qty * itemDto.Price;
                existingItem.UpdatedAt = DateTime.UtcNow;
                existingItem.UpdatedBy = User.Identity?.Name;
            }
        }

        // Пересчитываем общую сумму долга
        var newTotal = debt.Items.Sum(i => i.Total);
        var paidAmount = debt.OriginalAmount - debt.Amount; // Сколько уже оплачено
        
        debt.Amount = Math.Max(0, newTotal - paidAmount); // Новая сумма долга
        debt.OriginalAmount = newTotal; // Обновляем изначальную сумму

        if (debt.Amount <= 0)
        {
            debt.Status = DebtStatus.Paid;
        }

        await db.SaveChangesAsync(ct);

        return Ok(new { debt.Id, debt.Amount, debt.OriginalAmount, message = "Товары обновлены" });
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
        var pay = new DebtPayment 
        { 
            DebtId = id, 
            Amount = dto.Amount, 
            PaidAt = DateTime.UtcNow,
            Method = dto.PaymentMethod,
            Comment = dto.Notes,
            CreatedBy = User?.Identity?.Name
        };
        db.DebtPayments.Add(pay);
        debt.Amount -= dto.Amount;
        if (debt.Amount <= 0)
        {
            debt.Amount = 0;
            debt.Status = DebtStatus.Paid;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        // notify
        try { await debtsNotifier.NotifyDebtPaymentAsync(debt, pay, ct); } catch { /* ignore */ }
        return Ok(new { debt.Id, debt.Amount, debt.Status, payment = pay });
    }

    // POST /api/debts
    /// <summary>
    /// Создать долг с позициями
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> Create([FromBody] DebtCreateDto dto, CancellationToken ct)
    {
        if (dto == null) return ValidationProblem("Payload is required");
        if (dto.ClientId <= 0) return ValidationProblem("ClientId is required");
        if (dto.SaleId <= 0) return ValidationProblem("SaleId is required");
        if (dto.Items == null || dto.Items.Count == 0) return ValidationProblem("Items are required");

        var original = dto.Items.Sum(i => i.Qty * i.Price);

        var debt = new Debt
        {
            ClientId = dto.ClientId,
            SaleId = dto.SaleId,
            Amount = original,
            OriginalAmount = original,
            DueDate = dto.DueDate == default ? DateTime.UtcNow.AddDays(14) : dto.DueDate,
            Status = DebtStatus.Open,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User?.Identity?.Name
        };

        foreach (var it in dto.Items)
        {
            debt.Items.Add(new DebtItem
            {
                ProductId = it.ProductId,
                ProductName = it.ProductName,
                Sku = it.Sku,
                Qty = it.Qty,
                Price = it.Price,
                Total = it.Qty * it.Price,
                CreatedAt = DateTime.UtcNow
            });
        }

        db.Debts.Add(debt);
        await db.SaveChangesAsync(ct);

        return Created($"/api/debts/{debt.Id}", new { debt.Id });
    }
}
