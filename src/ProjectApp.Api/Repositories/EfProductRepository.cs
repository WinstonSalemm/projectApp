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

    public async Task<int> CountAsync(string? query, string? category = null, CancellationToken ct = default)
    {
        return await ApplyQuery(_db.Products.AsNoTracking(), query, category).CountAsync(ct);
    }

    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IEnumerable<Product>> SearchAsync(string? query, int page, int size, string? category = null, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 50;

        var items = await ApplyQuery(_db.Products.AsNoTracking(), query, category)
            .OrderBy(p => p.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return items;
    }

    private static IQueryable<Product> ApplyQuery(IQueryable<Product> products, string? query, string? category)
    {
        var res = products;
        if (!string.IsNullOrWhiteSpace(category))
        {
            var c = category.Trim();
            res = res.Where(p => p.Category == c);
        }
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            res = res.Where(p => EF.Functions.Like(p.Sku, $"%{q}%") || EF.Functions.Like(p.Name, $"%{q}%"));
        }
        return res;
    }

    public async Task<IEnumerable<string>> GetCategoriesAsync(CancellationToken ct = default)
    {
        return await _db.Products
            .AsNoTracking()
            .Select(p => p.Category)
            .Where(c => c != null && c != "")
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);
    }

    public async Task<Product> AddAsync(Product p, CancellationToken ct = default)
    {
        _db.Products.Add(p);
        await _db.SaveChangesAsync(ct);
        return p;
    }
}
