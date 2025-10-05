using ProjectApp.Api.Models;

namespace ProjectApp.Api.Repositories;

public interface ISaleRepository
{
    Task<Sale> AddAsync(Sale sale, CancellationToken ct = default);
    Task<Sale?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Sale>> QueryAsync(DateTime? dateFrom = null, DateTime? dateTo = null, string? createdBy = null, string? paymentType = null, int? clientId = null, CancellationToken ct = default);
}
