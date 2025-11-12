using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommissionsController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// Возвращает только комиссионных клиентов (партнёров)
    /// </summary>
    [HttpGet("agents")]
    [ProducesResponseType(typeof(IEnumerable<Client>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAgents()
    {
        var list = await db.Clients
            .AsNoTracking()
            .Where(c => c.IsCommissionAgent)
            .OrderByDescending(c => c.CommissionAgentSince)
            .ToListAsync();
        return Ok(list);
    }
}
