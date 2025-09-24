using ProjectApp.Api.Tests;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectApp.Api.Data;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Models;
using Xunit;

namespace ProjectApp.Api.Tests.Integration;

public class ReturnsTests : IClassFixture<IntegrationWebAppFactory>
{
    private readonly IntegrationWebAppFactory _factory;

    public ReturnsTests(IntegrationWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ReturnsFlow_FullReturn_RestoresRegister_ByPaymentType()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-KEY", "dev-key");

        var options = TestJson.Web;

        // Arrange initial stock for product 4
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var im40Before = await db.Stocks.Where(s => s.ProductId == 4 && s.Register == StockRegister.IM40)
                .Select(s => s.Qty).FirstAsync();
            var nd40Before = await db.Stocks.Where(s => s.ProductId == 4 && s.Register == StockRegister.ND40)
                .Select(s => s.Qty).FirstAsync();
            im40Before.Should().Be(100m);
            nd40Before.Should().Be(50m);
        }

        // Create sale with official payment -> IM40 deducted
        var saleDraft = new SaleCreateDto
        {
            ClientId = 1,
            ClientName = "Integration",
            PaymentType = PaymentType.CashWithReceipt,
            Items = [ new SaleCreateItemDto { ProductId = 4, Qty = 6m } ]
        };
        var createSale = await client.PostAsJsonAsync("/api/sales", saleDraft, TestJson.Web);
        createSale.StatusCode.Should().Be(HttpStatusCode.Created);
        var sale = await createSale.Content.ReadFromJsonAsync<Sale>(TestJson.Web);
        sale!.Id.Should().BeGreaterThan(0);

        // Perform return
        var retDto = new ReturnCreateDto { RefSaleId = sale.Id, ClientId = sale.ClientId };
        var createReturn = await client.PostAsJsonAsync("/api/returns", retDto, TestJson.Web);
        createReturn.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert stock restored
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var im40After = await db.Stocks.Where(s => s.ProductId == 4 && s.Register == StockRegister.IM40)
                .Select(s => s.Qty).FirstAsync();
            var nd40After = await db.Stocks.Where(s => s.ProductId == 4 && s.Register == StockRegister.ND40)
                .Select(s => s.Qty).FirstAsync();

            im40After.Should().Be(100m);
            nd40After.Should().Be(50m);
        }
    }
}


