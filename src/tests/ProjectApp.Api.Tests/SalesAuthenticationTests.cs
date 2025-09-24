using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ProjectApp.Api.Dtos;
using Xunit;

namespace ProjectApp.Api.Tests;

public class SalesAuthenticationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SalesAuthenticationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            // Force Development so dev-key is available and migrations/seed run
            builder.UseSetting("environment", "Development");
        });
    }

    [Fact]
    public async Task Post_Sales_Without_ApiKey_Returns_401()
    {
        var client = _factory.CreateClient();
        var payload = new SaleCreateDto
        {
            ClientId = 1,
            ClientName = "Test",
            PaymentType = ProjectApp.Api.Models.PaymentType.CashWithReceipt,
            Items = [ new SaleCreateItemDto { ProductId = 1, Qty = 1m } ]
        };

        var response = await client.PostAsJsonAsync("/api/sales", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Sales_With_Valid_ApiKey_Returns_201()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-KEY", "dev-key");

        var payload = new SaleCreateDto
        {
            ClientId = 1,
            ClientName = "Test",
            PaymentType = ProjectApp.Api.Models.PaymentType.CashWithReceipt,
            Items = [ new SaleCreateItemDto { ProductId = 1, Qty = 1m } ]
        };

        var response = await client.PostAsJsonAsync("/api/sales", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        // Optionally assert returned body contains sale with items
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotBeNullOrWhiteSpace();
    }
}
