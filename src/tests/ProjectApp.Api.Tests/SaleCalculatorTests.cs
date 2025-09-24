using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using ProjectApp.Api.Dtos;
using ProjectApp.Api.Repositories;
using ProjectApp.Api.Services;
using Xunit;

namespace ProjectApp.Api.Tests;

public class SaleCalculatorTests
{
    [Fact]
    public async Task BuildAndCalculateAsync_Computes_Total_For_Multiple_Items()
    {
        // Arrange: fresh DB with seeded products (prices)
        var fixture = new SqliteDbFixture();
        await using var db = fixture.CreateContext();
        var productRepo = new EfProductRepository(db);
        var calc = new SaleCalculator(productRepo);

        var dto = new SaleCreateDto
        {
            ClientId = 1,
            ClientName = "Test Client",
            PaymentType = ProjectApp.Api.Models.PaymentType.CashWithReceipt,
            Items =
            [
                new SaleCreateItemDto { ProductId = 1, Qty = 2m }, // Coffee 1kg @ 15.99 -> 31.98
                new SaleCreateItemDto { ProductId = 2, Qty = 3m }, // Tea 500g @ 8.49 -> 25.47
                new SaleCreateItemDto { ProductId = 3, Qty = 5m }, // Sugar 1kg @ 2.29 -> 11.45
            ]
        };

        // Act
        var sale = await calc.BuildAndCalculateAsync(dto, CancellationToken.None);

        // Assert
        sale.Items.Should().HaveCount(3);
        sale.Total.Should().Be(31.98m + 25.47m + 11.45m);
    }
}
