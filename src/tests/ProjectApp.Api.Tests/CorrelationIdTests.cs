using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ProjectApp.Api.Dtos;
using Xunit;
using System.Linq;

namespace ProjectApp.Api.Tests;

public class CorrelationIdTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CorrelationIdTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("environment", "Development");
        });
    }

    [Fact]
    public async Task Responses_Contain_Correlation_Id_Header()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
        response.Headers.TryGetValues("X-Correlation-ID", out var values1).Should().BeTrue();
        values1!.First().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProblemDetails_Contains_CorrelationId_Extension()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-KEY", "dev-key");

        // Send invalid sale draft to trigger validation problem (ArgumentException/InvalidOperationException)
        var payload = new SaleCreateDto
        {
            ClientId = 0, // invalid
            ClientName = string.Empty,
            PaymentType = ProjectApp.Api.Models.PaymentType.CashWithReceipt,
            Items = [] // empty
        };
        var response = await client.PostAsJsonAsync("/api/sales", payload);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("correlationId");

        response.Headers.TryGetValues("X-Correlation-ID", out var values2).Should().BeTrue();
        values2!.First().Should().NotBeNullOrWhiteSpace();
    }
}
