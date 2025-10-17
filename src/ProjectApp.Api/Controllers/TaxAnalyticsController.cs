using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectApp.Api.Services;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/tax-analytics")]
[Authorize(Policy = "RequireApiKey")]
public class TaxAnalyticsController : ControllerBase
{
    private readonly TaxCalculationService _taxService;

    public TaxAnalyticsController(TaxCalculationService taxService)
    {
        _taxService = taxService;
    }

    /// <summary>
    /// Получить налоговый отчет за период
    /// </summary>
    [HttpGet("report")]
    public async Task<IActionResult> GetTaxReport(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var report = await _taxService.CalculateTaxReportAsync(from, to);
        return Ok(report);
    }

    /// <summary>
    /// Получить налоговый отчет за месяц
    /// </summary>
    [HttpGet("report/monthly")]
    public async Task<IActionResult> GetMonthlyTaxReport(
        [FromQuery] int year,
        [FromQuery] int month)
    {
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1);
        
        var report = await _taxService.CalculateTaxReportAsync(from, to);
        await _taxService.SaveTaxRecordsAsync(report);
        
        return Ok(report);
    }

    /// <summary>
    /// Получить налоговый отчет за квартал
    /// </summary>
    [HttpGet("report/quarterly")]
    public async Task<IActionResult> GetQuarterlyTaxReport(
        [FromQuery] int year,
        [FromQuery] int quarter)
    {
        if (quarter < 1 || quarter > 4)
            return BadRequest("Quarter must be between 1 and 4");

        var from = new DateTime(year, (quarter - 1) * 3 + 1, 1);
        var to = from.AddMonths(3);
        
        var report = await _taxService.CalculateTaxReportAsync(from, to);
        return Ok(report);
    }

    /// <summary>
    /// Получить налоговый отчет за год
    /// </summary>
    [HttpGet("report/yearly")]
    public async Task<IActionResult> GetYearlyTaxReport([FromQuery] int year)
    {
        var from = new DateTime(year, 1, 1);
        var to = from.AddYears(1);
        
        var report = await _taxService.CalculateTaxReportAsync(from, to);
        return Ok(report);
    }

    /// <summary>
    /// Рассчитать НДС от суммы
    /// </summary>
    [HttpGet("calculate-vat")]
    public IActionResult CalculateVAT([FromQuery] decimal amountWithVAT)
    {
        var vat = _taxService.CalculateVAT(amountWithVAT);
        var amountWithoutVAT = _taxService.GetAmountWithoutVAT(amountWithVAT);
        
        return Ok(new
        {
            AmountWithVAT = amountWithVAT,
            AmountWithoutVAT = amountWithoutVAT,
            VATAmount = vat,
            VATRate = 12m
        });
    }

    /// <summary>
    /// Добавить НДС к сумме
    /// </summary>
    [HttpGet("add-vat")]
    public IActionResult AddVAT([FromQuery] decimal amountWithoutVAT)
    {
        var amountWithVAT = _taxService.AddVAT(amountWithoutVAT);
        var vat = amountWithVAT - amountWithoutVAT;
        
        return Ok(new
        {
            AmountWithoutVAT = amountWithoutVAT,
            AmountWithVAT = amountWithVAT,
            VATAmount = vat,
            VATRate = 12m
        });
    }

    /// <summary>
    /// Получить все неоплаченные налоги
    /// </summary>
    [HttpGet("unpaid")]
    public async Task<IActionResult> GetUnpaidTaxes()
    {
        var taxes = await _taxService.GetUnpaidTaxesAsync();
        return Ok(taxes);
    }

    /// <summary>
    /// Отметить налог как оплаченный
    /// </summary>
    [HttpPost("{id}/mark-paid")]
    public async Task<IActionResult> MarkTaxAsPaid(int id)
    {
        await _taxService.MarkTaxAsPaidAsync(id);
        return Ok(new { message = "Налог отмечен как оплаченный" });
    }

    /// <summary>
    /// Получить налоговые настройки
    /// </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetTaxSettings()
    {
        var settings = await _taxService.GetTaxSettingsAsync();
        return Ok(settings);
    }

    /// <summary>
    /// Получить налоговый календарь на месяц
    /// </summary>
    [HttpGet("calendar")]
    public async Task<IActionResult> GetTaxCalendar(
        [FromQuery] int year,
        [FromQuery] int month)
    {
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1);

        var calendar = new List<object>
        {
            new
            {
                Date = new DateTime(year, month, 15),
                Type = "Социальные налоги",
                Description = "Срок уплаты ЕСП, ИНПС, Школьный фонд",
                IsDeadline = true
            },
            new
            {
                Date = new DateTime(year, month, 20),
                Type = "НДС",
                Description = "Срок уплаты НДС за предыдущий месяц",
                IsDeadline = true
            },
            new
            {
                Date = new DateTime(year, month, 25),
                Type = "Налог на прибыль",
                Description = "Срок уплаты налога на прибыль (авансовый платеж)",
                IsDeadline = true
            }
        };

        // Добавляем фактические неоплаченные налоги
        var unpaidTaxes = await _taxService.GetUnpaidTaxesAsync();
        var monthTaxes = unpaidTaxes.Where(t => t.DueDate.Year == year && t.DueDate.Month == month);

        return Ok(new
        {
            Month = from.ToString("MMMM yyyy"),
            Calendar = calendar,
            UnpaidTaxes = monthTaxes
        });
    }
}
