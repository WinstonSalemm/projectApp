using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/debug")]
public class DebugController : ControllerBase
{
    private readonly AppDbContext _db;
    public DebugController(AppDbContext db) { _db = db; }

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
}
