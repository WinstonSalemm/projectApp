using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectApp.Api.Models;
using ProjectApp.Api.Services;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CashTransactionsController : ControllerBase
{
    private readonly CashboxService _service;

    public CashTransactionsController(CashboxService service)
    {
        _service = service;
    }

    /// <summary>
    /// Создать транзакцию
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CashTransaction), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CashTransactionCreateDto dto)
    {
        try
        {
            var transaction = new CashTransaction
            {
                Type = dto.Type,
                FromCashboxId = dto.FromCashboxId,
                ToCashboxId = dto.ToCashboxId,
                Amount = dto.Amount,
                Currency = dto.Currency ?? "UZS",
                Category = dto.Category,
                Description = dto.Description,
                CreatedBy = User.Identity?.Name ?? "system"
            };

            var created = await _service.CreateTransactionAsync(transaction);
            return CreatedAtAction(nameof(GetTransactions), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Получить историю транзакций
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CashTransaction>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int? cashboxId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] CashTransactionType? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var transactions = await _service.GetTransactionsAsync(cashboxId, from, to, type, page, pageSize);
        return Ok(transactions);
    }

    /// <summary>
    /// Отменить транзакцию
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Cancel(int id)
    {
        var cancelledBy = User.Identity?.Name ?? "system";
        await _service.CancelTransactionAsync(id, cancelledBy);
        return NoContent();
    }
}

public class CashTransactionCreateDto
{
    public CashTransactionType Type { get; set; }
    public int? FromCashboxId { get; set; }
    public int? ToCashboxId { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? Category { get; set; }
    public string Description { get; set; } = string.Empty;
}
