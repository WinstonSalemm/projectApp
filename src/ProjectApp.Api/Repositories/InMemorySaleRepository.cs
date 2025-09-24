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
}
