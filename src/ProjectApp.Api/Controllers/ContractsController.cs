using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Models;
using ProjectApp.Api.Repositories;
using ProjectApp.Api.Services;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContractsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ISaleRepository _sales;
    private readonly ISaleCalculator _calculator;
    private readonly ContractsService _contractsService;
    
    public ContractsController(AppDbContext db, ISaleRepository sales, ISaleCalculator calculator, ContractsService contractsService)
    {
        _db = db;
        _sales = sales;
        _calculator = calculator;
        _contractsService = contractsService;
    }

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
    [Authorize(Policy = "ManagerOnly")]
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
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        var c = await _db.Contracts
            .AsNoTracking()
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .Include(x => x.Deliveries)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
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
            TotalAmount = c.TotalAmount,
            PaidAmount = c.PaidAmount,
            TotalItemsCount = c.TotalItemsCount,
            DeliveredItemsCount = c.DeliveredItemsCount,
            Items = c.Items.Select(i => new ContractItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                Sku = i.Sku,
                Name = i.Name,
                Unit = i.Unit,
                Qty = i.Qty,
                DeliveredQty = i.DeliveredQty,
                UnitPrice = i.UnitPrice
            }).ToList(),
            Payments = c.Payments.Select(p => new ContractPaymentDto
            {
                Amount = p.Amount,
                Method = p.Method.ToString(),
                PaidAt = p.PaidAt,
                Note = p.Note
            }).OrderByDescending(p => p.PaidAt).ToList(),
            Deliveries = c.Deliveries.Select(d => new ContractDeliveryDto
            {
                ContractItemId = d.ContractItemId,
                Qty = d.Qty,
                DeliveredAt = d.DeliveredAt,
                Note = d.Note
            }).OrderByDescending(d => d.DeliveredAt).ToList()
        };
        return Ok(dto);
    }

    [HttpPost]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] ContractCreateDto dto, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        if (string.IsNullOrWhiteSpace(dto.OrgName))
            return ValidationProblem(detail: "OrgName is required");

        if (dto.Items == null || dto.Items.Count == 0)
            return ValidationProblem(detail: "At least one item is required");
        
        if (dto.Items.Any(i => i.Qty <= 0 || i.UnitPrice < 0))
            return ValidationProblem(detail: "Invalid item: Qty must be > 0 and UnitPrice >= 0");

        // Загружаем товары для заполнения SKU
        var productIds = dto.Items.Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value).ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var totalAmount = dto.Items.Sum(i => i.Qty * i.UnitPrice);
        
        var c = new Contract
        {
            OrgName = dto.OrgName.Trim(),
            Inn = string.IsNullOrWhiteSpace(dto.Inn) ? null : dto.Inn.Trim(),
            Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
            Status = ContractStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User?.Identity?.Name,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
            TotalAmount = totalAmount,
            TotalItemsCount = dto.Items.Count,
            Items = dto.Items.Select(i => new ContractItem
            {
                ProductId = i.ProductId,
                Sku = i.ProductId.HasValue && products.TryGetValue(i.ProductId.Value, out var p) ? p.Sku : "",
                Name = string.IsNullOrWhiteSpace(i.Name) ? "" : i.Name.Trim(),
                Unit = string.IsNullOrWhiteSpace(i.Unit) ? "шт" : i.Unit.Trim(),
                Qty = i.Qty,
                DeliveredQty = 0,
                UnitPrice = i.UnitPrice
            }).ToList()
        };
        _db.Contracts.Add(c);
        await _db.SaveChangesAsync(ct);

        return Created($"/api/contracts/{c.Id}", new { id = c.Id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] int id, [FromBody] ContractCreateDto dto, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        var c = await _db.Contracts.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();

        c.OrgName = string.IsNullOrWhiteSpace(dto.OrgName) ? c.OrgName : dto.OrgName.Trim();
        c.Inn = string.IsNullOrWhiteSpace(dto.Inn) ? null : dto.Inn.Trim();
        c.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
        c.Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        if (Enum.TryParse<ContractStatus>(dto.Status, true, out var newStatus))
            c.Status = newStatus;

        // Replace items
        _db.ContractItems.RemoveRange(c.Items);
        c.Items = dto.Items.Select(i => new ContractItem
        {
            ProductId = i.ProductId,
            Name = string.IsNullOrWhiteSpace(i.Name) ? string.Empty : i.Name.Trim(),
            Unit = string.IsNullOrWhiteSpace(i.Unit) ? "шт" : i.Unit.Trim(),
            Qty = i.Qty,
            UnitPrice = i.UnitPrice
        }).ToList();

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("{id:int}/status")]
    [Authorize(Policy = "ManagerOnly")]
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

    [HttpPost("{id:int}/payments")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddPayment([FromRoute] int id, [FromBody] ContractPaymentCreateDto dto, CancellationToken ct)
    {
        try
        {
            if (!Enum.TryParse<PaymentMethod>(dto.Method, true, out var method))
                return ValidationProblem(detail: "Invalid payment method");

            var userName = User?.Identity?.Name ?? "unknown";
            var payment = await _contractsService.AddPaymentAsync(id, dto.Amount, method, userName, dto.Note, ct);
            return Created($"/api/contracts/{id}/payments/{payment.Id}", payment);
        }
        catch (InvalidOperationException ex)
        {
            return ValidationProblem(detail: ex.Message);
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message);
        }
    }

    [HttpPost("{id:int}/deliveries")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeliverItem([FromRoute] int id, [FromBody] ContractDeliveryCreateDto dto, CancellationToken ct)
    {
        try
        {
            var userName = User?.Identity?.Name ?? "unknown";
            var delivery = await _contractsService.DeliverItemAsync(id, dto.ContractItemId, dto.Qty, userName, dto.Note, ct);
            return Created($"/api/contracts/{id}/deliveries/{delivery.Id}", delivery);
        }
        catch (InvalidOperationException ex)
        {
            return ValidationProblem(detail: ex.Message);
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message);
        }
    }

    [HttpPost("{id:int}/close")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CloseContract([FromRoute] int id, CancellationToken ct)
    {
        try
        {
            var userName = User?.Identity?.Name ?? "unknown";
            await _contractsService.CloseContractAsync(id, userName, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return ValidationProblem(detail: ex.Message);
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message);
        }
    }

    [HttpPost("{id:int}/items")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddItem([FromRoute] int id, [FromBody] ContractItemDto dto, CancellationToken ct)
    {
        try
        {
            var contract = await _db.Contracts.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (contract == null) return NotFound();

            if (contract.Status == ContractStatus.Closed || contract.Status == ContractStatus.Cancelled)
                return ValidationProblem(detail: "Cannot add items to closed or cancelled contract");

            // Загружаем товар если указан ProductId
            Product? product = null;
            if (dto.ProductId.HasValue)
            {
                product = await _db.Products.FirstOrDefaultAsync(p => p.Id == dto.ProductId.Value, ct);
            }

            var item = new ContractItem
            {
                ContractId = id,
                ProductId = dto.ProductId,
                Sku = product?.Sku ?? dto.Sku,
                Name = string.IsNullOrWhiteSpace(dto.Name) ? (product?.Name ?? "") : dto.Name,
                Unit = string.IsNullOrWhiteSpace(dto.Unit) ? "шт" : dto.Unit,
                Qty = dto.Qty,
                DeliveredQty = 0,
                UnitPrice = dto.UnitPrice
            };

            _db.ContractItems.Add(item);
            
            // Обновляем суммы
            contract.TotalAmount += item.Qty * item.UnitPrice;
            contract.TotalItemsCount++;
            
            await _db.SaveChangesAsync(ct);

            return Created($"/api/contracts/{id}/items/{item.Id}", new { id = item.Id });
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message);
        }
    }

    [HttpDelete("{id:int}/items/{itemId:int}")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteItem([FromRoute] int id, [FromRoute] int itemId, CancellationToken ct)
    {
        try
        {
            var contract = await _db.Contracts.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id, ct);
            if (contract == null) return NotFound();

            var item = contract.Items.FirstOrDefault(i => i.Id == itemId);
            if (item == null) return NotFound();

            if (item.DeliveredQty > 0)
                return ValidationProblem(detail: "Cannot delete item that has been partially delivered");

            if (contract.Status == ContractStatus.Closed || contract.Status == ContractStatus.Cancelled)
                return ValidationProblem(detail: "Cannot delete items from closed or cancelled contract");

            // Обновляем суммы
            contract.TotalAmount -= item.Qty * item.UnitPrice;
            contract.TotalItemsCount--;

            _db.ContractItems.Remove(item);
            await _db.SaveChangesAsync(ct);

            return NoContent();
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message);
        }
    }

    [HttpPut("{id:int}/items/{itemId:int}")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateItem([FromRoute] int id, [FromRoute] int itemId, [FromBody] ContractItemDto dto, CancellationToken ct)
    {
        try
        {
            var contract = await _db.Contracts.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id, ct);
            if (contract == null) return NotFound();

            var item = contract.Items.FirstOrDefault(i => i.Id == itemId);
            if (item == null) return NotFound();

            if (item.DeliveredQty > 0 && dto.Qty < item.DeliveredQty)
                return ValidationProblem(detail: $"Cannot set quantity below delivered amount ({item.DeliveredQty})");

            if (contract.Status == ContractStatus.Closed || contract.Status == ContractStatus.Cancelled)
                return ValidationProblem(detail: "Cannot update items in closed or cancelled contract");

            // Обновляем сумму договора
            var oldItemTotal = item.Qty * item.UnitPrice;
            var newItemTotal = dto.Qty * dto.UnitPrice;
            contract.TotalAmount = contract.TotalAmount - oldItemTotal + newItemTotal;

            // Обновляем позицию
            item.Qty = dto.Qty;
            item.UnitPrice = dto.UnitPrice;
            if (!string.IsNullOrWhiteSpace(dto.Name))
                item.Name = dto.Name;

            await _db.SaveChangesAsync(ct);

            return NoContent();
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message);
        }
    }
}
