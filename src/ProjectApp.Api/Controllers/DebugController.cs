using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Repositories;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/debug")]
public class DebugController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IProductRepository _productsRepo;
    public DebugController(AppDbContext db, IProductRepository productsRepo) { _db = db; _productsRepo = productsRepo; }

    [HttpGet("db")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDbInfo(CancellationToken ct)
    {
        var provider = _db.Database.ProviderName ?? string.Empty;
        var canConnect = await _db.Database.CanConnectAsync(ct);

        async Task<object> TryCountAsync(Func<Task<int>> f)
        {
            try { return new { ok = true, count = await f() }; }
            catch (Exception ex) { return new { ok = false, error = ex.Message }; }
        }

        var products = await TryCountAsync(() => _db.Products.CountAsync(ct));
        var stocks = await TryCountAsync(() => _db.Stocks.CountAsync(ct));
        var batches = await TryCountAsync(() => _db.Batches.CountAsync(ct));
        var users = await TryCountAsync(() => _db.Users.CountAsync(ct));

        return Ok(new { provider, canConnect, products, stocks, batches, users });
    }

    [HttpGet("products/categories")]
    [AllowAnonymous]
    public async Task<IActionResult> TestProductCategories(CancellationToken ct)
    {
        try
        {
            var cats = await _productsRepo.GetCategoriesAsync(ct);
            return Ok(new { ok = true, categories = cats });
        }
        catch (Exception ex)
        {
            return Ok(new { ok = false, error = ex.Message, stack = ex.StackTrace });
        }
    }

    [HttpGet("products/list")]
    [AllowAnonymous]
    public async Task<IActionResult> TestProducts([FromQuery] string? query, [FromQuery] int page = 1, [FromQuery] int size = 50, [FromQuery] string? category = null, CancellationToken ct = default)
    {
        try
        {
            var total = await _productsRepo.CountAsync(query, category, ct);
            var items = await _productsRepo.SearchAsync(query, page, size, category, ct);
            return Ok(new { ok = true, total, items = items.Select(p => new { p.Id, p.Sku, p.Name, p.Price, p.Category }) });
        }
        catch (Exception ex)
        {
            return Ok(new { ok = false, error = ex.Message, stack = ex.StackTrace });
        }
    }
}
