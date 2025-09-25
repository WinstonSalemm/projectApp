using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Models;
using ProjectApp.Api.Repositories;
using ProjectApp.Api.Services;
using ProjectApp.Api.Integrations.Telegram;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SalesController : ControllerBase
{
    private readonly ISaleRepository _sales;
    private readonly ISaleCalculator _calculator;
    private readonly ILogger<SalesController> _logger;
    private readonly ISalesNotifier _notifier;

    public SalesController(ISaleRepository sales, ISaleCalculator calculator, ILogger<SalesController> logger, ISalesNotifier notifier)
    {
        _sales = sales;
        _calculator = calculator;
        _logger = logger;
        _notifier = notifier;
    }

    [HttpPost]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(Sale), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] SaleCreateDto dto, CancellationToken ct)
    {
        try
        {
            var sale = await _calculator.BuildAndCalculateAsync(dto, ct);
            sale.CreatedBy = User?.Identity?.Name;
            sale = await _sales.AddAsync(sale, ct);

            _logger.LogInformation("Sale created {SaleId} for client {ClientId} total {Total} payment {PaymentType}",
                sale.Id, sale.ClientId, sale.Total, sale.PaymentType);

            // Fire-and-forget notification (do not block the response)
            _ = _notifier.NotifySaleAsync(sale, ct);

            var location = $"/api/sales/{sale.Id}";
            return Created(location, sale);
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(detail: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ValidationProblem(detail: ex.Message);
        }
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Sale), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken ct)
    {
        var sale = await _sales.GetByIdAsync(id, ct);
        if (sale is null) return NotFound();
        return Ok(sale);
    }
}

