using ProjectApp.Api.Models;

namespace ProjectApp.Api.Repositories;

public class InMemorySaleRepository : ISaleRepository
{
    private readonly List<Sale> _sales = new();
    private int _nextId = 1;

    public Task<Sale> AddAsync(Sale sale, CancellationToken ct = default)
    {
        sale.Id = _nextId++;
        _sales.Add(sale);
        return Task.FromResult(sale);
    }

    public Task<Sale?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return Task.FromResult(_sales.FirstOrDefault(x => x.Id == id));
    }

    public Task<IReadOnlyList<Sale>> QueryAsync(DateTime? dateFrom = null, DateTime? dateTo = null, string? createdBy = null, string? paymentType = null, int? clientId = null, CancellationToken ct = default)
    {
        IEnumerable<Sale> q = _sales;
        if (dateFrom.HasValue) q = q.Where(s => s.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue) q = q.Where(s => s.CreatedAt < dateTo.Value);
        if (!string.IsNullOrWhiteSpace(createdBy)) q = q.Where(s => string.Equals(s.CreatedBy, createdBy, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(paymentType) && Enum.TryParse<PaymentType>(paymentType, true, out var pt))
            q = q.Where(s => s.PaymentType == pt);
        if (clientId.HasValue) q = q.Where(s => s.ClientId == clientId.Value);
        return Task.FromResult((IReadOnlyList<Sale>)q.ToList());
    }
}
