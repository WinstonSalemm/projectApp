using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Repositories;

public class EfProductRepository : IProductRepository
{
    private readonly AppDbContext _db;
    private readonly ILogger<EfProductRepository> _logger;

    public EfProductRepository(AppDbContext db, ILogger<EfProductRepository> logger)
    {
        _db = db;
        _logger = logger;
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
        try
        {
            _logger.LogInformation("[EfProductRepository] SearchAsync: query={Query}, page={Page}, size={Size}, category={Category}", query, page, size, category);
            if (page < 1) page = 1;
            if (size < 1) size = 50;

            var items = await ApplyQuery(_db.Products.AsNoTracking(), query, category)
                .OrderBy(p => p.Id)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(ct);

            _logger.LogInformation("[EfProductRepository] SearchAsync: returned {Count} items", items.Count);
            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EfProductRepository] SearchAsync failed: {Message}", ex.Message);
            throw;
        }
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
        var cats = await _db.Products
            .AsNoTracking()
            .Select(p => p.Category)
            .ToListAsync(ct);

        var nonEmpty = cats
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c)
            .ToList();

        var hasEmpty = cats.Any(c => string.IsNullOrWhiteSpace(c));
        if (hasEmpty)
        {
            // Show a friendly placeholder for empty categories
            nonEmpty.Insert(0, "(Без категории)");
        }
        return nonEmpty;
    }

    public async Task<Product> AddAsync(Product p, CancellationToken ct = default)
    {
        _db.Products.Add(p);
        await _db.SaveChangesAsync(ct);
        return p;
    }
}
