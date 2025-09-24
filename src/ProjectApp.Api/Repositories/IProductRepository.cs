using ProjectApp.Api.Models;

namespace ProjectApp.Api.Repositories;

public interface IProductRepository
{
    Task<IEnumerable<Product>> SearchAsync(string? query, int page, int size, CancellationToken ct = default);
    Task<int> CountAsync(string? query, CancellationToken ct = default);
    Task<Product?> GetByIdAsync(int id, CancellationToken ct = default);
}
