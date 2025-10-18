using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectApp.Api.Models;
using ProjectApp.Api.Services;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/commission")]
[Authorize]
public class CommissionController : ControllerBase
{
    private readonly CommissionService _commissionService;
    private readonly ILogger<CommissionController> _logger;

    public CommissionController(
        CommissionService commissionService,
        ILogger<CommissionController> logger)
    {
        _commissionService = commissionService;
        _logger = logger;
    }

    /// <summary>
    /// Получить список всех партнеров
    /// </summary>
    [HttpGet("agents")]
    public async Task<IActionResult> GetAgents()
    {
        try
        {
            var agents = await _commissionService.GetCommissionAgentsAsync();
            return Ok(agents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения списка партнеров");
            return StatusCode(500, "Ошибка получения списка партнеров");
        }
    }

    /// <summary>
    /// Сделать клиента партнером
    /// </summary>
    [HttpPost("agents/{clientId}")]
    public async Task<IActionResult> MakeAgent(int clientId, [FromBody] MakeAgentRequest request)
    {
        try
        {
            var success = await _commissionService.MakeClientCommissionAgentAsync(
                clientId, 
                request.Notes);

            if (!success)
                return BadRequest("Не удалось сделать клиента партнером");

            return Ok(new { message = "Клиент стал партнером" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка создания партнера");
            return StatusCode(500, "Ошибка создания партнера");
        }
    }

    /// <summary>
    /// Убрать партнера (только если баланс = 0)
    /// </summary>
    [HttpDelete("agents/{clientId}")]
    public async Task<IActionResult> RemoveAgent(int clientId)
    {
        try
        {
            var (success, error) = await _commissionService.RemoveCommissionAgentAsync(clientId);

            if (!success)
                return BadRequest(new { error });

            return Ok(new { message = "Партнер удален" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка удаления партнера");
            return StatusCode(500, "Ошибка удаления партнера");
        }
    }

    /// <summary>
    /// Начислить комиссию за продажу
    /// </summary>
    [HttpPost("accrue/sale")]
    public async Task<IActionResult> AccrueSale([FromBody] AccrueSaleRequest request)
    {
        try
        {
            var success = await _commissionService.AccrueCommissionForSaleAsync(
                request.SaleId,
                request.CommissionAgentId,
                request.SaleTotal,
                request.CommissionRate,
                User.Identity?.Name);

            if (!success)
                return BadRequest("Не удалось начислить комиссию");

            return Ok(new { message = "Комиссия начислена" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка начисления комиссии");
            return StatusCode(500, "Ошибка начисления комиссии");
        }
    }

    /// <summary>
    /// Начислить комиссию за договор
    /// </summary>
    [HttpPost("accrue/contract")]
    public async Task<IActionResult> AccrueContract([FromBody] AccrueContractRequest request)
    {
        try
        {
            var success = await _commissionService.AccrueCommissionForContractAsync(
                request.ContractId,
                request.CommissionAgentId,
                request.CommissionAmount,
                User.Identity?.Name);

            if (!success)
                return BadRequest("Не удалось начислить комиссию");

            return Ok(new { message = "Комиссия начислена" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка начисления комиссии");
            return StatusCode(500, "Ошибка начисления комиссии");
        }
    }

    /// <summary>
    /// Выплатить комиссию деньгами
    /// </summary>
    [HttpPost("pay/cash")]
    public async Task<IActionResult> PayCash([FromBody] PayCashRequest request)
    {
        try
        {
            var (success, error) = await _commissionService.PayCommissionCashAsync(
                request.CommissionAgentId,
                request.Amount,
                request.IsCard,
                User.Identity?.Name,
                request.Notes);

            if (!success)
                return BadRequest(new { error });

            return Ok(new { message = "Комиссия выплачена" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка выплаты комиссии");
            return StatusCode(500, "Ошибка выплаты комиссии");
        }
    }

    /// <summary>
    /// Выплатить комиссию товаром (вызывается после создания продажи)
    /// </summary>
    [HttpPost("pay/product")]
    public async Task<IActionResult> PayProduct([FromBody] PayProductRequest request)
    {
        try
        {
            var (success, error) = await _commissionService.PayCommissionWithProductAsync(
                request.CommissionAgentId,
                request.SaleId,
                request.SaleTotal,
                User.Identity?.Name);

            if (!success)
                return BadRequest(new { error });

            return Ok(new { message = "Комиссия списана (выплата товаром)" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка выплаты комиссии товаром");
            return StatusCode(500, "Ошибка выплаты комиссии товаром");
        }
    }

    /// <summary>
    /// Получить историю транзакций партнера
    /// </summary>
    [HttpGet("agents/{agentId}/transactions")]
    public async Task<IActionResult> GetTransactions(
        int agentId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        try
        {
            var transactions = await _commissionService.GetAgentTransactionsAsync(
                agentId, 
                from, 
                to);

            return Ok(transactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения транзакций партнера");
            return StatusCode(500, "Ошибка получения транзакций");
        }
    }

    /// <summary>
    /// Получить статистику партнера
    /// </summary>
    [HttpGet("agents/{agentId}/stats")]
    public async Task<IActionResult> GetStats(int agentId)
    {
        try
        {
            var stats = await _commissionService.GetAgentStatsAsync(agentId);

            if (stats == null)
                return NotFound("Партнер не найден");

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения статистики партнера");
            return StatusCode(500, "Ошибка получения статистики");
        }
    }

    /// <summary>
    /// Получить общий отчет по всем партнерам
    /// </summary>
    [HttpGet("report")]
    public async Task<IActionResult> GetSummaryReport(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        try
        {
            var report = await _commissionService.GetSummaryReportAsync(from, to);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения отчета по партнерам");
            return StatusCode(500, "Ошибка получения отчета");
        }
    }
}

// ===== DTO для запросов =====

public class MakeAgentRequest
{
    public string? Notes { get; set; }
}

public class AccrueSaleRequest
{
    public int SaleId { get; set; }
    public int CommissionAgentId { get; set; }
    public decimal SaleTotal { get; set; }
    public decimal CommissionRate { get; set; }
}

public class AccrueContractRequest
{
    public int ContractId { get; set; }
    public int CommissionAgentId { get; set; }
    public decimal CommissionAmount { get; set; }
}

public class PayCashRequest
{
    public int CommissionAgentId { get; set; }
    public decimal Amount { get; set; }
    public bool IsCard { get; set; } // false = наличные, true = карта
    public string? Notes { get; set; }
}

public class PayProductRequest
{
    public int CommissionAgentId { get; set; }
    public int SaleId { get; set; }
    public decimal SaleTotal { get; set; }
}
