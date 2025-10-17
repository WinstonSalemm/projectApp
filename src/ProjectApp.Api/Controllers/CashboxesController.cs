using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectApp.Api.Models;
using ProjectApp.Api.Services;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CashboxesController : ControllerBase
{
    private readonly CashboxService _service;

    public CashboxesController(CashboxService service)
    {
        _service = service;
    }

    /// <summary>
    /// Получить список всех касс
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<Cashbox>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
    {
        var cashboxes = await _service.GetAllCashboxesAsync(includeInactive);
        return Ok(cashboxes);
    }

    /// <summary>
    /// Получить кассу по ID
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Cashbox), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var cashbox = await _service.GetCashboxByIdAsync(id);
        if (cashbox == null)
        {
            return NotFound();
        }
        return Ok(cashbox);
    }

    /// <summary>
    /// Создать новую кассу
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Cashbox), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] Cashbox cashbox)
    {
        var created = await _service.CreateCashboxAsync(cashbox);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Обновить кассу
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(Cashbox), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] Cashbox cashbox)
    {
        var existing = await _service.GetCashboxByIdAsync(id);
        if (existing == null)
        {
            return NotFound();
        }

        cashbox.Id = id;
        var updated = await _service.UpdateCashboxAsync(cashbox);
        return Ok(updated);
    }

    /// <summary>
    /// Деактивировать кассу
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _service.DeactivateCashboxAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Получить общий баланс всех касс по валютам
    /// </summary>
    [HttpGet("balances")]
    [ProducesResponseType(typeof(Dictionary<string, decimal>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTotalBalances()
    {
        var balances = await _service.GetTotalBalancesByCurrencyAsync();
        return Ok(balances);
    }
}
