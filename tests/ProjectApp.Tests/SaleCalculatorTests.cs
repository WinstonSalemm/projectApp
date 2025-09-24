using ProjectApp.Api.Dtos;
using ProjectApp.Api.Models;
using ProjectApp.Api.Repositories;
using ProjectApp.Api.Services;
using Xunit;

namespace ProjectApp.Tests;

public class SaleCalculatorTests
{
    private class FakeProductRepository : IProductRepository
    {
        private readonly Dictionary<int, Product> _data = new()
        {
            { 1, new Product { Id = 1, Sku = "A-1", Name = "Item A", Unit = "pc", Price = 10.00m } },
            { 2, new Product { Id = 2, Sku = "B-2", Name = "Item B", Unit = "pc", Price = 2.50m } },
        };

        public Task<int> CountAsync(string? query, CancellationToken ct = default) => Task.FromResult(0);
        public Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult(_data.TryGetValue(id, out var p) ? p : null);
        public Task<IEnumerable<Product>> SearchAsync(string? query, int page, int size, CancellationToken ct = default)
            => Task.FromResult<IEnumerable<Product>>(Array.Empty<Product>());
    }

    [Fact]
    public async Task BuildAndCalculateAsync_Computes_Total_From_Items()
    {
        var repo = new FakeProductRepository();
        var calc = new SaleCalculator(repo);

        var dto = new SaleCreateDto
        {
            ClientName = "John Doe",
            PaymentType = PaymentType.CashWithReceipt,
            Items = new()
            {
                new SaleCreateItemDto { ProductId = 1, Qty = 2 },
                new SaleCreateItemDto { ProductId = 2, Qty = 3.5m }
            }
        };

        var sale = await calc.BuildAndCalculateAsync(dto);

        var expected = 2 * 10.00m + 3.5m * 2.50m; // 20 + 8.75 = 28.75
        Assert.Equal(expected, sale.Total);
        Assert.Equal(2, sale.Items.Count);
        Assert.All(sale.Items, i => Assert.True(i.UnitPrice > 0));
    }
}
