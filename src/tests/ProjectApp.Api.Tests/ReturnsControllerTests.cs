using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Controllers;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Models;
using ProjectApp.Api.Repositories;
using ProjectApp.Api.Services;
using Xunit;

namespace ProjectApp.Api.Tests;

public class ReturnsControllerTests
{
    [Fact]
    public async Task Return_Increases_Stock_In_Correct_Register()
    {
        // Arrange fresh DB
        var fixture = new SqliteDbFixture();
        await using var db = fixture.CreateContext();

        var productRepo = new EfProductRepository(db);
        var saleCalc = new SaleCalculator(productRepo);
        var saleRepo = new EfSaleRepository(db);

        // Baseline stock for product 4
        var im40Before = await db.Stocks.Where(s => s.ProductId == 4 && s.Register == StockRegister.IM40)
            .Select(s => s.Qty).FirstAsync();
        var nd40Before = await db.Stocks.Where(s => s.ProductId == 4 && s.Register == StockRegister.ND40)
            .Select(s => s.Qty).FirstAsync();

        // Create a sale with official payment -> IM40 should be deducted
        var saleDto = new SaleCreateDto
        {
            ClientId = 1,
            ClientName = "Client",
            PaymentType = PaymentType.CashWithReceipt,
            Items = [ new SaleCreateItemDto { ProductId = 4, Qty = 6m } ]
        };
        var sale = await saleCalc.BuildAndCalculateAsync(saleDto, CancellationToken.None);
        sale = await saleRepo.AddAsync(sale, CancellationToken.None);

        var im40AfterSale = await db.Stocks.Where(s => s.ProductId == 4 && s.Register == StockRegister.IM40)
            .Select(s => s.Qty).FirstAsync();
        var nd40AfterSale = await db.Stocks.Where(s => s.ProductId == 4 && s.Register == StockRegister.ND40)
            .Select(s => s.Qty).FirstAsync();

        im40AfterSale.Should().Be(im40Before - 6m);
        nd40AfterSale.Should().Be(nd40Before);

        // Act: perform return for the sale
        var controller = new ReturnsController(db, NullLogger<ReturnsController>.Instance);
        var dto = new ReturnCreateDto { RefSaleId = sale.Id, ClientId = sale.ClientId };
        var result = await controller.Create(dto, CancellationToken.None);

        // Assert controller result
        result.Should().BeOfType<CreatedResult>();
        var im40AfterReturn = await db.Stocks.Where(s => s.ProductId == 4 && s.Register == StockRegister.IM40)
            .Select(s => s.Qty).FirstAsync();
        var nd40AfterReturn = await db.Stocks.Where(s => s.ProductId == 4 && s.Register == StockRegister.ND40)
            .Select(s => s.Qty).FirstAsync();

        // IM40 should be restored by +6
        im40AfterReturn.Should().Be(im40Before);
        nd40AfterReturn.Should().Be(nd40Before);
    }
}

