using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Repositories;

public class EfProductRepository : IProductRepository
{
    private readonly AppDbContext _db;

    public EfProductRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> CountAsync(string? query, CancellationToken ct = default)
    {
        return await ApplyQuery(_db.Products.AsNoTracking(), query).CountAsync(ct);
    }

    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IEnumerable<Product>> SearchAsync(string? query, int page, int size, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 50;

        var items = await ApplyQuery(_db.Products.AsNoTracking(), query)
            .OrderBy(p => p.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return items;
    }

    private static IQueryable<Product> ApplyQuery(IQueryable<Product> products, string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return products;
        var q = query.Trim();
        return products.Where(p =>
            EF.Functions.Like(p.Sku, $"%{q}%") ||
            EF.Functions.Like(p.Name, $"%{q}%"));
    }
}
