using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReturnsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<ReturnsController> _logger;

    public ReturnsController(AppDbContext db, ILogger<ReturnsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost]
    [Authorize(Policy = "RequireApiKey")]
    [ProducesResponseType(typeof(Return), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] ReturnCreateDto dto, CancellationToken ct)
    {
        try
        {
            if (dto.RefSaleId <= 0)
                return ValidationProblem(detail: "RefSaleId is required for return in v1 (full-sale return only)");

            var sale = await _db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == dto.RefSaleId, ct);
            if (sale is null)
                return ValidationProblem(detail: $"Sale not found: {dto.RefSaleId}");

            var register = MapPaymentToRegister(sale.PaymentType);

            // Increase stock back for full sale quantities
            foreach (var it in sale.Items)
            {
                var stock = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == it.ProductId && s.Register == register, ct);
                if (stock is null)
                {
                    // If stock record missing, create it with returned qty
                    stock = new Stock { ProductId = it.ProductId, Register = register, Qty = 0m };
                    _db.Stocks.Add(stock);
                }
                stock.Qty += it.Qty;
            }

            var ret = new Return
            {
                RefSaleId = sale.Id,
                ClientId = sale.ClientId,
                Sum = sale.Total,
                CreatedAt = DateTime.UtcNow
            };

            _db.Returns.Add(ret);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Return created {ReturnId} for sale {SaleId} client {ClientId} sum {Sum} payment {PaymentType} register {Register}",
                ret.Id, sale.Id, sale.ClientId, ret.Sum, sale.PaymentType, register);

            var location = $"/api/returns/{ret.Id}";
            return Created(location, ret);
        }
        catch (Exception ex)
        {
            return ValidationProblem(detail: ex.Message);
        }
    }

    private static StockRegister MapPaymentToRegister(PaymentType payment)
    {
        return payment switch
        {
            PaymentType.CashWithReceipt or PaymentType.CardWithReceipt or PaymentType.Site or PaymentType.Exchange => StockRegister.IM40,
            PaymentType.CashNoReceipt or PaymentType.Click or PaymentType.Payme => StockRegister.ND40,
            PaymentType.Credit => StockRegister.IM40,
            _ => StockRegister.IM40
        };
    }
}
