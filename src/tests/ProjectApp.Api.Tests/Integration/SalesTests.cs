using ProjectApp.Api.Tests;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ProjectApp.Api.Data;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Models;
using Xunit;

namespace ProjectApp.Api.Tests.Integration;

public class SalesTests : IClassFixture<IntegrationWebAppFactory>
{
    private readonly IntegrationWebAppFactory _factory;

    public SalesTests(IntegrationWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ApiKey_Required_ForMutations()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var draft = new SaleCreateDto
        {
            ClientId = 1,
            ClientName = "Integration",
            PaymentType = PaymentType.CashWithReceipt,
            Items = [ new SaleCreateItemDto { ProductId = 1, Qty = 1m } ]
        };

        // Without key -> 401
        var resNoKey = await client.PostAsJsonAsync("/api/sales", draft, TestJson.Web);
        resNoKey.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // With key -> 201
        client.DefaultRequestHeaders.Add("X-API-KEY", "dev-key");
        var resWithKey = await client.PostAsJsonAsync("/api/sales", draft, TestJson.Web);
        resWithKey.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task SalesFlow_CreateReturns201_ThenGetById_ReturnsSale()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-KEY", "dev-key");

        var draft = new SaleCreateDto
        {
            ClientId = 1,
            ClientName = "Integration",
            PaymentType = PaymentType.CashWithReceipt,
            Items = [ new SaleCreateItemDto { ProductId = 2, Qty = 3m } ]
        };

        var create = await client.PostAsJsonAsync("/api/sales", draft, TestJson.Web);
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        // Extract id from response body
        var options = TestJson.Web;
        var sale = await create.Content.ReadFromJsonAsync<Sale>(TestJson.Web);
        sale.Should().NotBeNull();
        sale!.Id.Should().BeGreaterThan(0);

        var get = await client.GetAsync($"/api/sales/{sale.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var saleById = await get.Content.ReadFromJsonAsync<Sale>(TestJson.Web);
        saleById!.Id.Should().Be(sale.Id);
        saleById.Items.Should().NotBeNull();
        saleById.Items.Count.Should().Be(1);
    }
}


