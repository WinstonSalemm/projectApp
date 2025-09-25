using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockController(AppDbContext db) : ControllerBase
{
    public record StockAvailabilityDto
    (
        string Key,
        decimal TotalQty,
        decimal Im40Qty,
        decimal Nd40Qty
    );

    // GET api/stock/available?skus=SKU-001,SKU-002
    [HttpGet("available")]
    [ProducesResponseType(typeof(IEnumerable<StockAvailabilityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableBySkus([FromQuery] string? skus)
    {
        if (string.IsNullOrWhiteSpace(skus))
            return Ok(Array.Empty<StockAvailabilityDto>());

        var skuList = skus.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                          .Select(s => s.ToUpperInvariant())
                          .ToArray();
        if (skuList.Length == 0)
            return Ok(Array.Empty<StockAvailabilityDto>());

        var query = from p in db.Products.AsNoTracking()
                    where skuList.Contains(p.Sku.ToUpper())
                    join s in db.Stocks.AsNoTracking() on p.Id equals s.ProductId
                    group s by p.Sku into g
                    select new StockAvailabilityDto(
                        Key: g.Key,
                        TotalQty: g.Where(x => x.Register == Models.StockRegister.IM40 || x.Register == Models.StockRegister.ND40).Sum(x => x.Qty),
                        Im40Qty: g.Where(x => x.Register == Models.StockRegister.IM40).Sum(x => x.Qty),
                        Nd40Qty: g.Where(x => x.Register == Models.StockRegister.ND40).Sum(x => x.Qty)
                    );

        var result = await query.ToListAsync();
        return Ok(result);
    }

    // GET api/stock/available/by-product-ids?ids=1,2
    [HttpGet("available/by-product-ids")]
    [ProducesResponseType(typeof(IEnumerable<StockAvailabilityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableByProductIds([FromQuery] string? ids)
    {
        if (string.IsNullOrWhiteSpace(ids))
            return Ok(Array.Empty<StockAvailabilityDto>());

        var idList = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(s => int.TryParse(s, out var v) ? v : (int?)null)
                        .Where(v => v.HasValue)
                        .Select(v => v!.Value)
                        .ToArray();
        if (idList.Length == 0)
            return Ok(Array.Empty<StockAvailabilityDto>());

        var query = from s in db.Stocks.AsNoTracking()
                    where idList.Contains(s.ProductId)
                    group s by s.ProductId into g
                    select new StockAvailabilityDto(
                        Key: g.Key.ToString(),
                        TotalQty: g.Where(x => x.Register == Models.StockRegister.IM40 || x.Register == Models.StockRegister.ND40).Sum(x => x.Qty),
                        Im40Qty: g.Where(x => x.Register == Models.StockRegister.IM40).Sum(x => x.Qty),
                        Nd40Qty: g.Where(x => x.Register == Models.StockRegister.ND40).Sum(x => x.Qty)
                    );

        var result = await query.ToListAsync();
        return Ok(result);
    }
}
