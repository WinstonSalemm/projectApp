using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    [HttpGet]
    [Authorize(Policy = "ManagerOnly")] // Admin or Manager
    [ProducesResponseType(typeof(IEnumerable<Sale>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo, [FromQuery] string? createdBy, [FromQuery] string? paymentType, [FromQuery] int? clientId, [FromQuery] bool all = false, CancellationToken ct = default)
    {
        var isAdmin = User.IsInRole("Admin");
        var allowAll = isAdmin || all;
        var effectiveCreatedBy = allowAll ? createdBy : (User?.Identity?.Name ?? createdBy);
        var list = await _sales.QueryAsync(dateFrom, dateTo, effectiveCreatedBy, paymentType, clientId, ct);
        return Ok(list);
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
        catch (DbUpdateException ex)
        {
            // Surface DB constraint issues (e.g., identity/keys/length) to caller as 400 for faster diagnosis
            var msg = ex.InnerException?.Message ?? ex.Message;
            return ValidationProblem(detail: msg);
        }
        catch (Exception ex)
        {
            // As a temporary measure, return details to help diagnose persistent 500s
            var msg = ex.InnerException?.Message ?? ex.Message;
            _logger.LogError(ex, "Error creating sale");
            return ValidationProblem(detail: msg);
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

