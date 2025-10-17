using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectApp.Api.Services;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ManagerOnly")]
public class CommercialAnalyticsController : ControllerBase
{
    private readonly ABCAnalysisService _abcAnalysis;
    private readonly DemandForecastService _forecast;
    private readonly PromotionService _promotions;
    private readonly DiscountValidationService _discountValidation;
    private readonly ILogger<CommercialAnalyticsController> _logger;

    public CommercialAnalyticsController(
        ABCAnalysisService abcAnalysis,
        DemandForecastService forecast,
        PromotionService promotions,
        DiscountValidationService discountValidation,
        ILogger<CommercialAnalyticsController> logger)
    {
        _abcAnalysis = abcAnalysis;
        _forecast = forecast;
        _promotions = promotions;
        _discountValidation = discountValidation;
        _logger = logger;
    }

    /// <summary>
    /// ABC-анализ товаров
    /// </summary>
    [HttpGet("abc")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ABCAnalysis([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        try
        {
            var dateFrom = from ?? DateTime.UtcNow.AddDays(-90);
            var dateTo = to ?? DateTime.UtcNow;

            var results = await _abcAnalysis.AnalyzeAsync(dateFrom, dateTo, ct);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CommercialAnalytics] ABC analysis failed");
            return Problem(detail: ex.Message);
        }
    }

    /// <summary>
    /// Рекомендации по категории C
    /// </summary>
    [HttpGet("abc/recommendations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ABCRecommendations([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        try
        {
            var dateFrom = from ?? DateTime.UtcNow.AddDays(-90);
            var dateTo = to ?? DateTime.UtcNow;

            var recommendations = await _abcAnalysis.GetRecommendationsAsync(dateFrom, dateTo, ct);
            return Ok(recommendations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CommercialAnalytics] ABC recommendations failed");
            return Problem(detail: ex.Message);
        }
    }

    /// <summary>
    /// Прогноз спроса
    /// </summary>
    [HttpGet("forecast")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DemandForecast([FromQuery] int days = 30, CancellationToken ct = default)
    {
        try
        {
            var results = await _forecast.ForecastAsync(days, ct);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CommercialAnalytics] Demand forecast failed");
            return Problem(detail: ex.Message);
        }
    }

    /// <summary>
    /// Критичные товары (дефицит или скоро дефицит)
    /// </summary>
    [HttpGet("forecast/critical")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CriticalProducts(CancellationToken ct)
    {
        try
        {
            var results = await _forecast.GetCriticalProductsAsync(ct);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CommercialAnalytics] Critical products failed");
            return Problem(detail: ex.Message);
        }
    }

    /// <summary>
    /// Рекомендации по закупке
    /// </summary>
    [HttpGet("forecast/purchase-recommendations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> PurchaseRecommendations([FromQuery] int days = 30, CancellationToken ct = default)
    {
        try
        {
            var results = await _forecast.GetPurchaseRecommendationsAsync(days, ct);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CommercialAnalytics] Purchase recommendations failed");
            return Problem(detail: ex.Message);
        }
    }

    /// <summary>
    /// Автоматически создать акции
    /// </summary>
    [HttpPost("promotions/auto-generate")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AutoGeneratePromotions(CancellationToken ct)
    {
        try
        {
            var userName = User?.Identity?.Name ?? "system";
            var promotions = await _promotions.AutoGeneratePromotionsAsync(userName, ct);
            return Ok(promotions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CommercialAnalytics] Auto-generate promotions failed");
            return Problem(detail: ex.Message);
        }
    }

    /// <summary>
    /// Получить активные акции
    /// </summary>
    [HttpGet("promotions/active")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivePromotions(CancellationToken ct)
    {
        try
        {
            var promotions = await _promotions.GetActivePromotionsAsync(ct);
            return Ok(promotions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CommercialAnalytics] Get active promotions failed");
            return Problem(detail: ex.Message);
        }
    }

    /// <summary>
    /// Валидировать скидку
    /// </summary>
    [HttpPost("discounts/validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateDiscount(
        [FromQuery] int productId,
        [FromQuery] decimal unitPrice,
        [FromQuery] decimal discountPercent,
        [FromQuery] int? clientId,
        CancellationToken ct)
    {
        try
        {
            var result = await _discountValidation.ValidateDiscountAsync(productId, unitPrice, discountPercent, clientId, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CommercialAnalytics] Validate discount failed");
            return Problem(detail: ex.Message);
        }
    }

    /// <summary>
    /// Рекомендуемая цена с учетом маржи
    /// </summary>
    [HttpGet("discounts/recommended-price")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecommendedPrice(
        [FromQuery] int productId,
        [FromQuery] decimal targetMargin = 15m,
        CancellationToken ct = default)
    {
        try
        {
            var price = await _discountValidation.GetRecommendedPriceAsync(productId, targetMargin, ct);
            return Ok(new { productId, targetMargin, recommendedPrice = price });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CommercialAnalytics] Get recommended price failed");
            return Problem(detail: ex.Message);
        }
    }

    /// <summary>
    /// Анализ скидок за период
    /// </summary>
    [HttpGet("discounts/analysis")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AnalyzeDiscounts(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default)
    {
        try
        {
            var dateFrom = from ?? DateTime.UtcNow.AddDays(-30);
            var dateTo = to ?? DateTime.UtcNow;

            var analysis = await _discountValidation.AnalyzeDiscountsAsync(dateFrom, dateTo, ct);
            return Ok(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CommercialAnalytics] Analyze discounts failed");
            return Problem(detail: ex.Message);
        }
    }
}
