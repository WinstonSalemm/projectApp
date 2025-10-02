using ProjectApp.Api.Models;

namespace ProjectApp.Api.Repositories;

public interface IProductRepository
{
    Task<IEnumerable<Product>> SearchAsync(string? query, int page, int size, string? category = null, CancellationToken ct = default);
    Task<int> CountAsync(string? query, string? category = null, CancellationToken ct = default);
    Task<Product?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<string>> GetCategoriesAsync(CancellationToken ct = default);
    Task<Product> AddAsync(Product p, CancellationToken ct = default);
}
