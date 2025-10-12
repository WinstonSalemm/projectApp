using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;

namespace ProjectApp.Api.Modules.Finance.Clients;

public class ClientFinanceReportBuilder(AppDbContext db)
{
    private readonly AppDbContext _db = db;

    public async Task<ClientFinanceDto> BuildAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var rows = await (from s in _db.Sales.AsNoTracking()
                          where s.CreatedAt >= fromUtc && s.CreatedAt < toUtc
                          join i in _db.SaleItems.AsNoTracking() on s.Id equals i.SaleId
                          join c in _db.Clients.AsNoTracking() on s.ClientId equals c.Id into gj
                          from c in gj.DefaultIfEmpty()
                          group new { s, i, c } by new { s.ClientId, Name = c != null ? c.Name : "Без клиента" } into g
                          select new ClientFinanceRow
                          {
                              ClientId = g.Key.ClientId ?? 0,
                              Name = g.Key.Name,
                              Revenue = g.Sum(x => x.i.UnitPrice * x.i.Qty),
                              Gross = g.Sum(x => x.i.UnitPrice * x.i.Qty) - g.Sum(x => x.i.Cost * x.i.Qty),
                              SalesCount = g.Select(x => x.s.Id).Distinct().Count()
                          }).OrderByDescending(r => r.Revenue).ToListAsync(ct);
        return new ClientFinanceDto { Clients = rows };
    }
}
