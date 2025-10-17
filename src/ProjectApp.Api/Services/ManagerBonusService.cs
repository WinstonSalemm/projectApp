using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Services;

public class ManagerBonusService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ManagerBonusService> _logger;
    private readonly BonusSettings _settings;

    public ManagerBonusService(AppDbContext db, ILogger<ManagerBonusService> logger)
    {
        _db = db;
        _logger = logger;
        
        // Настройки по умолчанию (можно вынести в appsettings.json)
        _settings = new BonusSettings
        {
            BasePercent = 3.0m,
            OwnClientsPercent = 5.0m,
            MinimumSales = 5000000m, // 5 млн сум минимум
            Enabled = true
        };
    }

    /// <summary>
    /// Рассчитать бонусы за указанный месяц
    /// </summary>
    public async Task<List<ManagerBonus>> CalculateBonusesAsync(int year, int month, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("[ManagerBonusService] Bonus system is disabled");
            return new List<ManagerBonus>();
        }

        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);

        _logger.LogInformation("[ManagerBonusService] Calculating bonuses for {Year}-{Month:D2}", year, month);

        // Получаем все продажи за период
        var sales = await _db.Sales
            .Include(s => s.Items)
            .Where(s => s.CreatedAt >= startDate && s.CreatedAt < endDate)
            .ToListAsync(ct);

        // Группируем по менеджерам
        var managerSales = sales
            .Where(s => !string.IsNullOrWhiteSpace(s.CreatedBy))
            .GroupBy(s => s.CreatedBy!)
            .ToList();

        var bonuses = new List<ManagerBonus>();

        foreach (var group in managerSales)
        {
            var userName = group.Key;
            var managerSalesData = group.ToList();

            // Общий оборот
            var totalSales = managerSalesData.Sum(s => s.Total);

            // Оборот по своим клиентам (где client.OwnerUserName == userName)
            var ownClientsSales = 0m;
            var ownClientsIds = new HashSet<int>();

            foreach (var sale in managerSalesData.Where(s => s.ClientId.HasValue))
            {
                var client = await _db.Clients
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == sale.ClientId!.Value, ct);

                if (client != null && 
                    !string.IsNullOrWhiteSpace(client.OwnerUserName) &&
                    string.Equals(client.OwnerUserName, userName, StringComparison.OrdinalIgnoreCase))
                {
                    ownClientsSales += sale.Total;
                    ownClientsIds.Add(client.Id);
                }
            }

            // Расчет бонуса
            decimal bonusAmount = 0m;

            if (totalSales >= _settings.MinimumSales)
            {
                // Бонус за общий оборот
                var baseSales = totalSales - ownClientsSales;
                bonusAmount += baseSales * (_settings.BasePercent / 100);

                // Бонус за своих клиентов (повышенный %)
                bonusAmount += ownClientsSales * (_settings.OwnClientsPercent / 100);
            }

            var bonus = new ManagerBonus
            {
                UserName = userName,
                Year = year,
                Month = month,
                TotalSales = totalSales,
                OwnClientsSales = ownClientsSales,
                BonusAmount = decimal.Round(bonusAmount, 2),
                BonusPercent = totalSales > 0 ? decimal.Round((bonusAmount / totalSales) * 100, 2) : 0,
                SalesCount = managerSalesData.Count,
                OwnClientsCount = ownClientsIds.Count,
                IsPaid = false,
                CalculatedAt = DateTime.UtcNow
            };

            bonuses.Add(bonus);

            _logger.LogInformation(
                "[ManagerBonusService] {UserName}: Sales={TotalSales:N0}, OwnClients={OwnClientsSales:N0}, Bonus={BonusAmount:N0}",
                userName, totalSales, ownClientsSales, bonusAmount);
        }

        return bonuses;
    }

    /// <summary>
    /// Сохранить рассчитанные бонусы
    /// </summary>
    public async Task SaveBonusesAsync(List<ManagerBonus> bonuses, CancellationToken ct = default)
    {
        foreach (var bonus in bonuses)
        {
            // Проверяем существует ли уже бонус за этот период
            var existing = await _db.ManagerBonuses
                .FirstOrDefaultAsync(b => 
                    b.UserName == bonus.UserName && 
                    b.Year == bonus.Year && 
                    b.Month == bonus.Month, ct);

            if (existing != null)
            {
                // Обновляем существующий
                existing.TotalSales = bonus.TotalSales;
                existing.OwnClientsSales = bonus.OwnClientsSales;
                existing.BonusAmount = bonus.BonusAmount;
                existing.BonusPercent = bonus.BonusPercent;
                existing.SalesCount = bonus.SalesCount;
                existing.OwnClientsCount = bonus.OwnClientsCount;
                existing.CalculatedAt = bonus.CalculatedAt;
            }
            else
            {
                // Создаем новый
                _db.ManagerBonuses.Add(bonus);
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("[ManagerBonusService] Saved {Count} bonuses", bonuses.Count);
    }

    /// <summary>
    /// Отметить бонус как выплаченный
    /// </summary>
    public async Task MarkAsPaidAsync(int bonusId, CancellationToken ct = default)
    {
        var bonus = await _db.ManagerBonuses.FindAsync(new object[] { bonusId }, ct);
        if (bonus != null)
        {
            bonus.IsPaid = true;
            bonus.PaidAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Получить бонусы за период
    /// </summary>
    public async Task<List<ManagerBonus>> GetBonusesAsync(int year, int month, CancellationToken ct = default)
    {
        return await _db.ManagerBonuses
            .Where(b => b.Year == year && b.Month == month)
            .OrderByDescending(b => b.BonusAmount)
            .ToListAsync(ct);
    }
}
