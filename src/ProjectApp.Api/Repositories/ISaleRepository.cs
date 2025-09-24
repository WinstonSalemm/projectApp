using ProjectApp.Api.Models;

namespace ProjectApp.Api.Repositories;

public interface ISaleRepository
{
    Task<Sale> AddAsync(Sale sale, CancellationToken ct = default);
    Task<Sale?> GetByIdAsync(int id, CancellationToken ct = default);
}
