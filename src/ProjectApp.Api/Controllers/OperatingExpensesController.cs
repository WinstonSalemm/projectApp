using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectApp.Api.Models;
using ProjectApp.Api.Services;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OperatingExpensesController : ControllerBase
{
    private readonly OperatingExpensesService _service;

    public OperatingExpensesController(OperatingExpensesService service)
    {
        _service = service;
    }

    /// <summary>
    /// Создать операционный расход
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OperatingExpense), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] OperatingExpenseCreateDto dto)
    {
        var expense = new OperatingExpense
        {
            Type = dto.Type,
            Amount = dto.Amount,
            Currency = dto.Currency ?? "UZS",
            ExpenseDate = dto.ExpenseDate ?? DateTime.UtcNow,
            Description = dto.Description,
            Category = dto.Category,
            IsRecurring = dto.IsRecurring,
            RecurringPeriod = dto.RecurringPeriod,
            CashboxId = dto.CashboxId,
            Recipient = dto.Recipient,
            PaymentStatus = dto.PaymentStatus,
            CreatedBy = User.Identity?.Name ?? "system"
        };

        var created = await _service.CreateExpenseAsync(expense, createCashTransaction: dto.CreateCashTransaction);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Получить список расходов
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<OperatingExpense>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] ExpenseType? type = null,
        [FromQuery] ExpensePaymentStatus? status = null,
        [FromQuery] int? cashboxId = null)
    {
        var expenses = await _service.GetExpensesAsync(from, to, type, status, cashboxId);
        return Ok(expenses);
    }

    /// <summary>
    /// Получить расход по ID
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OperatingExpense), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var expense = await _service.GetExpenseByIdAsync(id);
        if (expense == null)
        {
            return NotFound();
        }
        return Ok(expense);
    }

    /// <summary>
    /// Пометить расход как оплаченный
    /// </summary>
    [HttpPost("{id:int}/pay")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MarkAsPaid(int id, [FromBody] MarkAsPaidDto dto)
    {
        try
        {
            var paidBy = User.Identity?.Name ?? "system";
            await _service.MarkAsPaidAsync(id, dto.CashboxId, paidBy);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Получить сумму расходов за период
    /// </summary>
    [HttpGet("total")]
    [ProducesResponseType(typeof(decimal), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTotal(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] ExpenseType? type = null)
    {
        var total = await _service.GetTotalExpensesAsync(from, to, type);
        return Ok(total);
    }

    /// <summary>
    /// Получить расходы по типам
    /// </summary>
    [HttpGet("by-type")]
    [ProducesResponseType(typeof(Dictionary<ExpenseType, decimal>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByType(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var expenses = await _service.GetExpensesByTypeAsync(from, to);
        return Ok(expenses);
    }

    /// <summary>
    /// Получить регулярные расходы
    /// </summary>
    [HttpGet("recurring")]
    [ProducesResponseType(typeof(List<OperatingExpense>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecurring()
    {
        var expenses = await _service.GetRecurringExpensesAsync();
        return Ok(expenses);
    }
}

public class OperatingExpenseCreateDto
{
    public ExpenseType Type { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public DateTime? ExpenseDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsRecurring { get; set; }
    public RecurringPeriod? RecurringPeriod { get; set; }
    public int? CashboxId { get; set; }
    public string? Recipient { get; set; }
    public ExpensePaymentStatus PaymentStatus { get; set; } = ExpensePaymentStatus.Paid;
    public bool CreateCashTransaction { get; set; } = true;
}

public class MarkAsPaidDto
{
    public int CashboxId { get; set; }
}
