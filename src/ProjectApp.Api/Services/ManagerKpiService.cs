using ProjectApp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ProjectApp.Api.Services;

public class ManagerKpiDto
{
    public string ManagerUserName { get; set; } = string.Empty;
    public int SalesCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageCheck { get; set; }
    public int ReservationsCount { get; set; }
    public decimal ConversionRate { get; set; } // Продажи / Брони
    public int ClientsCount { get; set; }
    public decimal BonusAmount { get; set; }
    public int Rank { get; set; } // Место в рейтинге
    public decimal EfficiencyScore { get; set; } // Общий балл эффективности
}

/// <summary>
/// Сервис для расчета KPI менеджеров
/// </summary>
public class ManagerKpiService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ManagerKpiService> _logger;

    public ManagerKpiService(AppDbContext db, ILogger<ManagerKpiService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Получить KPI всех менеджеров за период
    /// </summary>
    public async Task<List<ManagerKpiDto>> GetAllManagersKpiAsync(DateTime from, DateTime to)
    {
        try
        {
            var startDate = from;
            var endDate = to;
            
            // Продажи менеджеров
            var salesStats = await (from s in _db.Sales
                                   where s.CreatedAt >= startDate && s.CreatedAt < endDate && s.CreatedBy != null
                                   group s by s.CreatedBy into g
                                   select new
                                   {
                                       Manager = g.Key!,
                                       SalesCount = g.Count(),
                                       TotalRevenue = g.Sum(s => s.Total),
                                       AvgCheck = g.Average(s => s.Total)
                                   }).ToListAsync();

            // Брони менеджеров
            var reservationsStats = await (from r in _db.Reservations
                                          where r.CreatedAt >= startDate && r.CreatedAt < endDate && r.CreatedBy != null
                                          group r by r.CreatedBy into g
                                          select new
                                          {
                                              Manager = g.Key!,
                                              ReservationsCount = g.Count()
                                          }).ToListAsync();

            // Клиенты менеджеров
            var clientsStats = await (from c in _db.Clients
                                     where c.OwnerUserName != null
                                     group c by c.OwnerUserName into g
                                     select new
                                     {
                                         Manager = g.Key!,
                                         ClientsCount = g.Count()
                                     }).ToListAsync();

            // Бонусы менеджеров (за весь период)
            var bonusStats = await (from b in _db.ManagerBonuses
                                   group b by b.ManagerUserName into g
                                   select new
                                   {
                                       Manager = g.Key,
                                       TotalBonus = g.Sum(b => b.BonusAmount)
                                   }).ToListAsync();

            // Объединяем данные
            var allManagers = salesStats.Select(s => s.Manager)
                .Union(reservationsStats.Select(r => r.Manager))
                .Union(clientsStats.Select(c => c.Manager))
                .Distinct()
                .ToList();

            var kpiList = new List<ManagerKpiDto>();

            foreach (var manager in allManagers)
            {
                var sales = salesStats.FirstOrDefault(s => s.Manager == manager);
                var reservations = reservationsStats.FirstOrDefault(r => r.Manager == manager);
                var clients = clientsStats.FirstOrDefault(c => c.Manager == manager);
                var bonus = bonusStats.FirstOrDefault(b => b.Manager == manager);

                var salesCount = sales?.SalesCount ?? 0;
                var reservationsCount = reservations?.ReservationsCount ?? 0;
                var conversionRate = reservationsCount > 0 ? (decimal)salesCount / reservationsCount * 100 : 0;

                // Расчет общего балла эффективности
                // (Выручка * 0.4) + (Конверсия * 0.3) + (Средний чек * 0.2) + (Кол-во клиентов * 0.1)
                var revenueScore = (sales?.TotalRevenue ?? 0) / 1000000; // Нормализация
                var conversionScore = conversionRate / 10; // Нормализация
                var avgCheckScore = (sales?.AvgCheck ?? 0) / 100000; // Нормализация
                var clientsScore = (clients?.ClientsCount ?? 0) / 10m; // Нормализация

                var efficiencyScore = (revenueScore * 0.4m) + (conversionScore * 0.3m) + 
                                     (avgCheckScore * 0.2m) + (clientsScore * 0.1m);

                kpiList.Add(new ManagerKpiDto
                {
                    ManagerUserName = manager,
                    SalesCount = salesCount,
                    TotalRevenue = sales?.TotalRevenue ?? 0,
                    AverageCheck = sales?.AvgCheck ?? 0,
                    ReservationsCount = reservationsCount,
                    ConversionRate = conversionRate,
                    ClientsCount = clients?.ClientsCount ?? 0,
                    BonusAmount = bonus?.TotalBonus ?? 0,
                    EfficiencyScore = efficiencyScore
                });
            }

            // Присваиваем рейтинг (места)
            var ranked = kpiList.OrderByDescending(k => k.EfficiencyScore).ToList();
            for (int i = 0; i < ranked.Count; i++)
            {
                ranked[i].Rank = i + 1;
            }

            return ranked;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка расчета KPI менеджеров");
            return new List<ManagerKpiDto>();
        }
    }

    /// <summary>
    /// Получить KPI конкретного менеджера
    /// </summary>
    public async Task<ManagerKpiDto?> GetManagerKpiAsync(string managerUserName, DateTime from, DateTime to)
    {
        var all = await GetAllManagersKpiAsync(from, to);
        return all.FirstOrDefault(k => k.ManagerUserName == managerUserName);
    }

    /// <summary>
    /// Получить топ менеджеров
    /// </summary>
    public async Task<List<ManagerKpiDto>> GetTopManagersAsync(DateTime from, DateTime to, int top = 5)
    {
        var all = await GetAllManagersKpiAsync(from, to);
        return all.OrderByDescending(k => k.EfficiencyScore).Take(top).ToList();
    }
}
