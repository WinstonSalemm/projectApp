using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class ContractsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ContractsController(AppDbContext db) { _db = db; }

    // Self-healing: ensure Contracts schema exists in prod if migrations/patchers didn't run
    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        try
        {
            var provider = _db.Database.ProviderName ?? string.Empty;

            if (provider.Contains("MySql", StringComparison.OrdinalIgnoreCase))
            {
                // MySQL: create tables idempotently
                await _db.Database.ExecuteSqlRawAsync(@"CREATE TABLE IF NOT EXISTS `Contracts` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `OrgName` VARCHAR(256) NOT NULL,
  `Inn` VARCHAR(32) NULL,
  `Phone` VARCHAR(32) NULL,
  `Status` INT NOT NULL,
  `CreatedAt` DATETIME(6) NOT NULL,
  `Note` VARCHAR(1024) NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;", ct);

                await _db.Database.ExecuteSqlRawAsync(@"CREATE TABLE IF NOT EXISTS `ContractItems` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `ContractId` INT NOT NULL,
  `ProductId` INT NULL,
  `Name` VARCHAR(256) NOT NULL,
  `Unit` VARCHAR(16) NOT NULL,
  `Qty` DECIMAL(18,3) NOT NULL,
  `UnitPrice` DECIMAL(18,2) NOT NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_ContractItems_ContractId` (`ContractId` ASC),
  CONSTRAINT `FK_ContractItems_Contracts_ContractId` FOREIGN KEY (`ContractId`) REFERENCES `Contracts` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;", ct);
            }
            else if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                await _db.Database.ExecuteSqlRawAsync(@"CREATE TABLE IF NOT EXISTS Contracts (
  Id INTEGER NOT NULL CONSTRAINT PK_Contracts PRIMARY KEY AUTOINCREMENT,
  OrgName TEXT NOT NULL,
  Inn TEXT NULL,
  Phone TEXT NULL,
  Status INTEGER NOT NULL,
  CreatedAt TEXT NOT NULL,
  Note TEXT NULL
);", ct);

                await _db.Database.ExecuteSqlRawAsync(@"CREATE TABLE IF NOT EXISTS ContractItems (
  Id INTEGER NOT NULL CONSTRAINT PK_ContractItems PRIMARY KEY AUTOINCREMENT,
  ContractId INTEGER NOT NULL,
  ProductId INTEGER NULL,
  Name TEXT NOT NULL,
  Unit TEXT NOT NULL,
  Qty DECIMAL(18,3) NOT NULL,
  UnitPrice DECIMAL(18,2) NOT NULL,
  CONSTRAINT FK_ContractItems_Contracts_ContractId FOREIGN KEY (ContractId) REFERENCES Contracts (Id) ON DELETE CASCADE
);", ct);
            }
        }
        catch
        {
            // swallow: controller actions will still try/catch as needed
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ContractDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        var q = _db.Contracts.AsNoTracking().Include(c => c.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ContractStatus>(status, true, out var st))
            q = q.Where(c => c.Status == st);

        var list = await q.OrderByDescending(c => c.CreatedAt)
            .Select(c => new ContractDto
            {
                Id = c.Id,
                OrgName = c.OrgName,
                Inn = c.Inn,
                Phone = c.Phone,
                Status = c.Status.ToString(),
                CreatedAt = c.CreatedAt,
                Note = c.Note,
                Items = c.Items.Select(i => new ContractItemDto
                {
                    ProductId = i.ProductId,
                    Name = i.Name,
                    Unit = i.Unit,
                    Qty = i.Qty,
                    UnitPrice = i.UnitPrice
                }).ToList()
            }).ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        var c = await _db.Contracts.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();
        var dto = new ContractDto
        {
            Id = c.Id,
            OrgName = c.OrgName,
            Inn = c.Inn,
            Phone = c.Phone,
            Status = c.Status.ToString(),
            CreatedAt = c.CreatedAt,
            Note = c.Note,
            Items = c.Items.Select(i => new ContractItemDto
            {
                ProductId = i.ProductId,
                Name = i.Name,
                Unit = i.Unit,
                Qty = i.Qty,
                UnitPrice = i.UnitPrice
            }).ToList()
        };
        return Ok(dto);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] ContractCreateDto dto, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        if (string.IsNullOrWhiteSpace(dto.OrgName))
            return ValidationProblem(detail: "OrgName is required");

        var status = Enum.TryParse<ContractStatus>(dto.Status, true, out var st) ? st : ContractStatus.Signed;
        var c = new Contract
        {
            OrgName = dto.OrgName.Trim(),
            Inn = string.IsNullOrWhiteSpace(dto.Inn) ? null : dto.Inn.Trim(),
            Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
            Status = status,
            CreatedAt = DateTime.UtcNow,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
            Items = dto.Items.Select(i => new ContractItem
            {
                ProductId = i.ProductId,
                Name = string.IsNullOrWhiteSpace(i.Name) ? string.Empty : i.Name.Trim(),
                Unit = string.IsNullOrWhiteSpace(i.Unit) ? "шт" : i.Unit.Trim(),
                Qty = i.Qty,
                UnitPrice = i.UnitPrice
            }).ToList()
        };
        _db.Contracts.Add(c);
        await _db.SaveChangesAsync(ct);

        return Created($"/api/contracts/{c.Id}", new { id = c.Id });
    }

    [HttpPut("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus([FromRoute] int id, [FromBody] ContractUpdateStatusDto dto, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        var c = await _db.Contracts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();
        if (!Enum.TryParse<ContractStatus>(dto.Status, true, out var st))
            return ValidationProblem(detail: "Invalid status");
        c.Status = st;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("ensure-schema")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> EnsureSchema(CancellationToken ct)
    {
        try
        {
            var provider = _db.Database.ProviderName ?? string.Empty;
            await EnsureSchemaAsync(ct);
            return Ok(new { provider, ensured = true });
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message);
        }
    }
}
