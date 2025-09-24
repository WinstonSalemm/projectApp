using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Models;
using ProjectApp.Api.Repositories;
using ProjectApp.Api.Services;
using ProjectApp.Api.Dtos;
using Xunit;

namespace ProjectApp.Api.Tests;

public class StockRegisterSelectionTests
{
    private static async Task<(EfSaleRepository repo, EfProductRepository products, ProjectApp.Api.Data.AppDbContext db)> BuildAsync()
    {
        var fixture = new SqliteDbFixture();
        var db = fixture.CreateContext();
        var repo = new EfSaleRepository(db);
        var products = new EfProductRepository(db);
        return (repo, products, db);
    }

    [Fact]
    public async Task OfficialPayments_Deduct_From_IM40()
    {
        var (repo, products, db) = await BuildAsync();
        await using var _ = db;
        var calc = new SaleCalculator(products);

        // initial stock
        var im40Before = await db.Stocks.Where(s => s.ProductId == 1 && s.Register == StockRegister.IM40).Select(s => s.Qty).FirstAsync();
        var nd40Before = await db.Stocks.Where(s => s.ProductId == 1 && s.Register == StockRegister.ND40).Select(s => s.Qty).FirstAsync();

        var dto = new SaleCreateDto
        {
            ClientId = 1,
            ClientName = "Client",
            PaymentType = PaymentType.CashWithReceipt, // official
            Items = [ new SaleCreateItemDto { ProductId = 1, Qty = 10m } ]
        };
        var sale = await calc.BuildAndCalculateAsync(dto, CancellationToken.None);
        await repo.AddAsync(sale, CancellationToken.None);

        var im40After = await db.Stocks.Where(s => s.ProductId == 1 && s.Register == StockRegister.IM40).Select(s => s.Qty).FirstAsync();
        var nd40After = await db.Stocks.Where(s => s.ProductId == 1 && s.Register == StockRegister.ND40).Select(s => s.Qty).FirstAsync();

        im40After.Should().Be(im40Before - 10m);
        nd40After.Should().Be(nd40Before);
    }

    [Fact]
    public async Task GreyPayments_Deduct_From_ND40()
    {
        var (repo, products, db) = await BuildAsync();
        await using var _ = db;
        var calc = new SaleCalculator(products);

        var im40Before = await db.Stocks.Where(s => s.ProductId == 2 && s.Register == StockRegister.IM40).Select(s => s.Qty).FirstAsync();
        var nd40Before = await db.Stocks.Where(s => s.ProductId == 2 && s.Register == StockRegister.ND40).Select(s => s.Qty).FirstAsync();

        var dto = new SaleCreateDto
        {
            ClientId = 1,
            ClientName = "Client",
            PaymentType = PaymentType.CashNoReceipt, // grey
            Items = [ new SaleCreateItemDto { ProductId = 2, Qty = 7m } ]
        };
        var sale = await calc.BuildAndCalculateAsync(dto, CancellationToken.None);
        await repo.AddAsync(sale, CancellationToken.None);

        var im40After = await db.Stocks.Where(s => s.ProductId == 2 && s.Register == StockRegister.IM40).Select(s => s.Qty).FirstAsync();
        var nd40After = await db.Stocks.Where(s => s.ProductId == 2 && s.Register == StockRegister.ND40).Select(s => s.Qty).FirstAsync();

        nd40After.Should().Be(nd40Before - 7m);
        im40After.Should().Be(im40Before);
    }

    [Fact]
    public async Task Credit_Payments_Deduct_From_IM40()
    {
        var (repo, products, db) = await BuildAsync();
        await using var _ = db;
        var calc = new SaleCalculator(products);

        var im40Before = await db.Stocks.Where(s => s.ProductId == 3 && s.Register == StockRegister.IM40).Select(s => s.Qty).FirstAsync();
        var nd40Before = await db.Stocks.Where(s => s.ProductId == 3 && s.Register == StockRegister.ND40).Select(s => s.Qty).FirstAsync();

        var dto = new SaleCreateDto
        {
            ClientId = 1,
            ClientName = "Client",
            PaymentType = PaymentType.Credit,
            Items = [ new SaleCreateItemDto { ProductId = 3, Qty = 12m } ]
        };
        var sale = await calc.BuildAndCalculateAsync(dto, CancellationToken.None);
        await repo.AddAsync(sale, CancellationToken.None);

        var im40After = await db.Stocks.Where(s => s.ProductId == 3 && s.Register == StockRegister.IM40).Select(s => s.Qty).FirstAsync();
        var nd40After = await db.Stocks.Where(s => s.ProductId == 3 && s.Register == StockRegister.ND40).Select(s => s.Qty).FirstAsync();

        im40After.Should().Be(im40Before - 12m);
        nd40After.Should().Be(nd40Before);
    }
}
