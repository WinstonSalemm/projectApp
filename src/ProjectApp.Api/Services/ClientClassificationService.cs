using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Services;

public class ClientClassificationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ClientClassificationService> _logger;

    // Пороги для классификации (в сумах)
    private const decimal RETAIL_THRESHOLD = 10_000_000m;      // до 10 млн - розница
    private const decimal WHOLESALE_THRESHOLD = 50_000_000m;   // 10-50 млн - опт
    // свыше 50 млн - крупный опт

    public ClientClassificationService(AppDbContext db, ILogger<ClientClassificationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Обновить статистику клиента после продажи
    /// </summary>
    public async Task UpdateClientStatsAsync(int clientId, decimal saleAmount, CancellationToken ct = default)
    {
        var client = await _db.Clients.FindAsync(new object[] { clientId }, ct);
        if (client == null) return;

        client.TotalPurchases += saleAmount;
        client.PurchasesCount++;
        client.LastPurchaseDate = DateTime.UtcNow;

        // Автоматическая классификация
        await ClassifyClientAsync(client);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[ClientClassification] Updated client {ClientId}: Total={Total:N0}, Type={Type}",
            clientId, client.TotalPurchases, client.Type);
    }

    /// <summary>
    /// Классифицировать клиента по объему покупок
    /// </summary>
    private Task ClassifyClientAsync(Client client)
    {
        var oldType = client.Type;
        ClientType newType;

        if (client.TotalPurchases < RETAIL_THRESHOLD)
        {
            newType = ClientType.Retail;
        }
        else if (client.TotalPurchases < WHOLESALE_THRESHOLD)
        {
            newType = ClientType.Wholesale;
        }
        else
        {
            newType = ClientType.LargeWholesale;
        }

        if (client.Type != newType)
        {
            client.Type = newType;
            client.TypeAssignedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "[ClientClassification] Client {ClientId} type changed: {OldType} → {NewType} (Total: {Total:N0})",
                client.Id, oldType, newType, client.TotalPurchases);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Пересчитать типы для всех клиентов
    /// </summary>
    public async Task ReclassifyAllClientsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[ClientClassification] Starting reclassification of all clients");

        var clients = await _db.Clients.ToListAsync(ct);
        int updated = 0;

        foreach (var client in clients)
        {
            // Пересчитываем статистику из продаж
            var sales = await _db.Sales
                .Where(s => s.ClientId == client.Id)
                .ToListAsync(ct);

            client.TotalPurchases = sales.Sum(s => s.Total);
            client.PurchasesCount = sales.Count;
            client.LastPurchaseDate = sales.Any() ? sales.Max(s => s.CreatedAt) : null;

            var oldType = client.Type;
            await ClassifyClientAsync(client);

            if (oldType != client.Type)
            {
                updated++;
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[ClientClassification] Reclassification complete. Updated {Count} clients", updated);
    }

    /// <summary>
    /// Получить статистику по типам клиентов
    /// </summary>
    public async Task<Dictionary<ClientType, (int Count, decimal TotalSales)>> GetTypeStatisticsAsync(CancellationToken ct = default)
    {
        var stats = await _db.Clients
            .GroupBy(c => c.Type)
            .Select(g => new
            {
                Type = g.Key,
                Count = g.Count(),
                TotalSales = g.Sum(c => c.TotalPurchases)
            })
            .ToListAsync(ct);

        return stats.ToDictionary(
            s => s.Type,
            s => (s.Count, s.TotalSales)
        );
    }
}
