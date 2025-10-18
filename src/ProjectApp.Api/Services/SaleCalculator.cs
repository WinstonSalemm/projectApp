using ProjectApp.Api.Dtos;
using ProjectApp.Api.Models;
using ProjectApp.Api.Repositories;

namespace ProjectApp.Api.Services;

public class SaleCalculator : ISaleCalculator
{
    private readonly IProductRepository _products;

    public SaleCalculator(IProductRepository products)
    {
        _products = products;
    }

    public async Task<Sale> BuildAndCalculateAsync(SaleCreateDto dto, CancellationToken ct = default)
    {
        if (dto is null) throw new ArgumentNullException(nameof(dto));
        if (string.IsNullOrWhiteSpace(dto.ClientName)) throw new ArgumentException("ClientName is required", nameof(dto));
        if (dto.Items is null || dto.Items.Count == 0) throw new ArgumentException("At least one item is required", nameof(dto));

        var sale = new Sale
        {
            ClientId = dto.ClientId,
            ClientName = dto.ClientName.Trim(),
            PaymentType = dto.PaymentType,
            CreatedAt = DateTime.UtcNow,
            CommissionAgentId = dto.CommissionAgentId,
            CommissionRate = dto.CommissionRate
        };

        foreach (var it in dto.Items)
        {
            if (it.Qty <= 0) throw new ArgumentException($"Qty must be > 0 for ProductId={it.ProductId}");
            var product = await _products.GetByIdAsync(it.ProductId, ct);
            if (product is null) throw new ArgumentException($"Product not found: {it.ProductId}");
            sale.Items.Add(new SaleItem
            {
                ProductId = product.Id,
                Qty = it.Qty,
                UnitPrice = it.UnitPrice
            });
        }

        sale.Total = sale.Items.Sum(i => i.Qty * i.UnitPrice);
        return sale;
    }
}
