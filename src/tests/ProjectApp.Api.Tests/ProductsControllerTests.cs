using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using ProjectApp.Api.Controllers;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Repositories;
using Xunit;

namespace ProjectApp.Api.Tests;

public class ProductsControllerTests
{
    [Fact]
    public async Task Get_Returns_PagedResult_Of_Dto_With_Pagination()
    {
        // Arrange fresh DB (in-memory sqlite with migrations + seed from OnModelCreating)
        var fixture = new SqliteDbFixture();
        await using var db = fixture.CreateContext();

        var repo = new EfProductRepository(db);
        var controller = new ProductsController(repo);

        // Act
        var action = await controller.Get(query: null, page: 1, size: 5, ct: CancellationToken.None);

        // Assert
        var ok = action.Result as OkObjectResult;
        ok.Should().NotBeNull();
        var payload = ok!.Value as PagedResult<ProductDto>;
        payload.Should().NotBeNull();

        payload!.Page.Should().Be(1);
        payload.Size.Should().Be(5);
        payload.Total.Should().BeGreaterOrEqualTo(10); // OnModelCreating seeds 10
        payload.Items.Should().NotBeNull();
        payload.Items.Count.Should().Be(5);

        // Check DTO fields mapping for a known product (Id = 1)
        var firstDto = payload.Items.FirstOrDefault(i => i.Id == 1);
        firstDto.Should().NotBeNull();
        var prod = await repo.GetByIdAsync(1, CancellationToken.None);
        prod.Should().NotBeNull();
        firstDto!.Name.Should().Be(prod!.Name);
        firstDto.Sku.Should().Be(prod.Sku);
        firstDto.UnitPrice.Should().Be(prod.Price);
    }
}
