using ProjectApp.Api.Tests;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Repositories;
using Xunit;

namespace ProjectApp.Api.Tests.Integration;

public class ProductsTests : IClassFixture<IntegrationWebAppFactory>
{
    private readonly IntegrationWebAppFactory _factory;

    public ProductsTests(IntegrationWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProductsSearch_ReturnsDtoAndPagination()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products?page=1&size=5");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var options = TestJson.Web;
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<ProductDto>>(options);
        payload.Should().NotBeNull();
        payload!.Page.Should().Be(1);
        payload.Size.Should().Be(5);
        payload.Total.Should().BeGreaterOrEqualTo(10);
        payload.Items.Count.Should().Be(5);

        var firstDto = payload.Items.FirstOrDefault(i => i.Id == 1);
        firstDto.Should().NotBeNull();
        // We can optionally fetch via repository, but integration sticks to contract
        firstDto!.Name.Should().NotBeNullOrWhiteSpace();
        firstDto.Sku.Should().NotBeNullOrWhiteSpace();
        firstDto.UnitPrice.Should().BeGreaterThan(0);
    }
}

