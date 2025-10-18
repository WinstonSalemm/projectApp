using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Services;

/// <summary>
/// Сервис для работы с партнерской программой (комиссионными клиентами)
/// </summary>
public class CommissionService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CommissionService> _logger;

    public CommissionService(AppDbContext db, ILogger<CommissionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Получить список всех партнеров
    /// </summary>
    public async Task<List<Client>> GetCommissionAgentsAsync()
    {
        return await _db.Clients
            .Where(c => c.IsCommissionAgent)
            .OrderByDescending(c => c.CommissionAgentSince)
            .ToListAsync();
    }

    /// <summary>
    /// Сделать клиента партнером
    /// </summary>
    public async Task<bool> MakeClientCommissionAgentAsync(int clientId, string? notes = null)
    {
        var client = await _db.Clients.FindAsync(clientId);
        if (client == null) return false;

        if (client.IsCommissionAgent)
        {
            _logger.LogWarning("Клиент {ClientId} уже является партнером", clientId);
            return false;
        }

        client.IsCommissionAgent = true;
        client.CommissionAgentSince = DateTime.UtcNow;
        client.CommissionNotes = notes;
        client.CommissionBalance = 0;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Клиент {ClientId} стал партнером", clientId);
        return true;
    }

    /// <summary>
    /// Убрать партнера (только если баланс = 0)
    /// </summary>
    public async Task<(bool Success, string? Error)> RemoveCommissionAgentAsync(int clientId)
    {
        var client = await _db.Clients.FindAsync(clientId);
        if (client == null) return (false, "Клиент не найден");

        if (!client.IsCommissionAgent) return (false, "Клиент не является партнером");

        if (client.CommissionBalance != 0)
        {
            return (false, $"Невозможно убрать партнера. Баланс комиссии: {client.CommissionBalance:N2} UZS");
        }

        client.IsCommissionAgent = false;
        client.CommissionAgentSince = null;
        client.CommissionNotes = null;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Клиент {ClientId} больше не партнер", clientId);
        return (true, null);
    }

    /// <summary>
    /// Начислить комиссию за продажу
    /// </summary>
    public async Task<bool> AccrueCommissionForSaleAsync(
        int saleId, 
        int commissionAgentId, 
        decimal saleTotal,
        decimal commissionRate,
        string? createdBy = null)
    {
        var agent = await _db.Clients.FindAsync(commissionAgentId);
        if (agent == null || !agent.IsCommissionAgent)
        {
            _logger.LogWarning("Партнер {AgentId} не найден или не активен", commissionAgentId);
            return false;
        }

        // Рассчитываем сумму комиссии
        var commissionAmount = Math.Round(saleTotal * commissionRate / 100, 2);

        // Обновляем баланс партнера
        agent.CommissionBalance += commissionAmount;

        // Создаем транзакцию
        var transaction = new CommissionTransaction
        {
            CommissionAgentId = commissionAgentId,
            Type = CommissionTransactionType.Accrual,
            Amount = commissionAmount,
            BalanceAfter = agent.CommissionBalance,
            RelatedSaleId = saleId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            Notes = $"Начислена комиссия {commissionRate}% за продажу #{saleId}"
        };

        _db.CommissionTransactions.Add(transaction);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Начислена комиссия {Amount} партнеру {AgentId} за продажу {SaleId}",
            commissionAmount, commissionAgentId, saleId);

        return true;
    }

    /// <summary>
    /// Начислить комиссию за договор
    /// </summary>
    public async Task<bool> AccrueCommissionForContractAsync(
        int contractId,
        int commissionAgentId,
        decimal commissionAmount,
        string? createdBy = null)
    {
        var agent = await _db.Clients.FindAsync(commissionAgentId);
        if (agent == null || !agent.IsCommissionAgent)
        {
            _logger.LogWarning("Партнер {AgentId} не найден или не активен", commissionAgentId);
            return false;
        }

        // Обновляем баланс партнера
        agent.CommissionBalance += commissionAmount;

        // Создаем транзакцию
        var transaction = new CommissionTransaction
        {
            CommissionAgentId = commissionAgentId,
            Type = CommissionTransactionType.ContractAccrual,
            Amount = commissionAmount,
            BalanceAfter = agent.CommissionBalance,
            RelatedContractId = contractId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            Notes = $"Начислена комиссия за договор #{contractId}"
        };

        _db.CommissionTransactions.Add(transaction);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Начислена комиссия {Amount} партнеру {AgentId} за договор {ContractId}",
            commissionAmount, commissionAgentId, contractId);

        return true;
    }

    /// <summary>
    /// Выплатить комиссию деньгами (наличные или карта)
    /// </summary>
    public async Task<(bool Success, string? Error)> PayCommissionCashAsync(
        int commissionAgentId,
        decimal amount,
        bool isCard,
        string? createdBy = null,
        string? notes = null)
    {
        var agent = await _db.Clients.FindAsync(commissionAgentId);
        if (agent == null || !agent.IsCommissionAgent)
        {
            return (false, "Партнер не найден");
        }

        if (amount <= 0)
        {
            return (false, "Сумма должна быть больше 0");
        }

        if (amount > agent.CommissionBalance)
        {
            return (false, $"Сумма выплаты ({amount:N2}) больше баланса ({agent.CommissionBalance:N2})");
        }

        // Списываем с баланса
        agent.CommissionBalance -= amount;

        // Создаем транзакцию
        var transaction = new CommissionTransaction
        {
            CommissionAgentId = commissionAgentId,
            Type = isCard ? CommissionTransactionType.PaymentCard : CommissionTransactionType.PaymentCash,
            Amount = -amount, // Отрицательная сумма = списание
            BalanceAfter = agent.CommissionBalance,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            Notes = notes ?? $"Выплата комиссии {(isCard ? "на карту" : "наличными")}"
        };

        _db.CommissionTransactions.Add(transaction);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Выплачена комиссия {Amount} {Method} партнеру {AgentId}",
            amount, isCard ? "на карту" : "наличными", commissionAgentId);

        return (true, null);
    }

    /// <summary>
    /// Выплатить комиссию товаром (при создании продажи)
    /// </summary>
    public async Task<(bool Success, string? Error)> PayCommissionWithProductAsync(
        int commissionAgentId,
        int saleId,
        decimal saleTotal,
        string? createdBy = null)
    {
        var agent = await _db.Clients.FindAsync(commissionAgentId);
        if (agent == null || !agent.IsCommissionAgent)
        {
            return (false, "Партнер не найден");
        }

        if (saleTotal > agent.CommissionBalance)
        {
            return (false, $"Сумма продажи ({saleTotal:N2}) больше баланса ({agent.CommissionBalance:N2})");
        }

        // Списываем с баланса
        agent.CommissionBalance -= saleTotal;

        // Создаем транзакцию
        var transaction = new CommissionTransaction
        {
            CommissionAgentId = commissionAgentId,
            Type = CommissionTransactionType.PaymentProduct,
            Amount = -saleTotal, // Отрицательная сумма = списание
            BalanceAfter = agent.CommissionBalance,
            RelatedSaleId = saleId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            Notes = $"Выплата комиссии товаром (продажа #{saleId})"
        };

        _db.CommissionTransactions.Add(transaction);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Выплачена комиссия {Amount} товаром партнеру {AgentId} (продажа {SaleId})",
            saleTotal, commissionAgentId, saleId);

        return (true, null);
    }

    /// <summary>
    /// Получить историю транзакций партнера
    /// </summary>
    public async Task<List<CommissionTransaction>> GetAgentTransactionsAsync(
        int commissionAgentId,
        DateTime? from = null,
        DateTime? to = null)
    {
        var query = _db.CommissionTransactions
            .Where(t => t.CommissionAgentId == commissionAgentId);

        if (from.HasValue)
            query = query.Where(t => t.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(t => t.CreatedAt <= to.Value);

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Получить статистику партнера
    /// </summary>
    public async Task<CommissionAgentStats?> GetAgentStatsAsync(int commissionAgentId)
    {
        var agent = await _db.Clients.FindAsync(commissionAgentId);
        if (agent == null || !agent.IsCommissionAgent) return null;

        var transactions = await _db.CommissionTransactions
            .Where(t => t.CommissionAgentId == commissionAgentId)
            .ToListAsync();

        var totalAccrued = transactions
            .Where(t => t.Amount > 0)
            .Sum(t => t.Amount);

        var totalPaid = Math.Abs(transactions
            .Where(t => t.Amount < 0)
            .Sum(t => t.Amount));

        var salesCount = transactions
            .Where(t => t.Type == CommissionTransactionType.Accrual && t.RelatedSaleId.HasValue)
            .Count();

        var contractsCount = transactions
            .Where(t => t.Type == CommissionTransactionType.ContractAccrual && t.RelatedContractId.HasValue)
            .Count();

        return new CommissionAgentStats
        {
            AgentId = commissionAgentId,
            AgentName = agent.Name,
            CurrentBalance = agent.CommissionBalance,
            TotalAccrued = totalAccrued,
            TotalPaid = totalPaid,
            SalesCount = salesCount,
            ContractsCount = contractsCount,
            AgentSince = agent.CommissionAgentSince ?? DateTime.UtcNow
        };
    }

    /// <summary>
    /// Получить отчет по всем партнерам
    /// </summary>
    public async Task<CommissionSummaryReport> GetSummaryReportAsync(DateTime? from = null, DateTime? to = null)
    {
        var agents = await _db.Clients
            .Where(c => c.IsCommissionAgent)
            .ToListAsync();

        var agentStats = new List<CommissionAgentStats>();
        
        foreach (var agent in agents)
        {
            var stats = await GetAgentStatsAsync(agent.Id);
            if (stats != null)
            {
                agentStats.Add(stats);
            }
        }

        var totalBalance = agents.Sum(a => a.CommissionBalance);
        var totalAccrued = agentStats.Sum(s => s.TotalAccrued);
        var totalPaid = agentStats.Sum(s => s.TotalPaid);

        return new CommissionSummaryReport
        {
            TotalAgents = agents.Count,
            TotalBalance = totalBalance,
            TotalAccrued = totalAccrued,
            TotalPaid = totalPaid,
            Agents = agentStats.OrderByDescending(s => s.CurrentBalance).ToList()
        };
    }
}

/// <summary>
/// Статистика партнера
/// </summary>
public class CommissionAgentStats
{
    public int AgentId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public decimal TotalAccrued { get; set; }
    public decimal TotalPaid { get; set; }
    public int SalesCount { get; set; }
    public int ContractsCount { get; set; }
    public DateTime AgentSince { get; set; }
}

/// <summary>
/// Общий отчет по всем партнерам
/// </summary>
public class CommissionSummaryReport
{
    public int TotalAgents { get; set; }
    public decimal TotalBalance { get; set; }
    public decimal TotalAccrued { get; set; }
    public decimal TotalPaid { get; set; }
    public List<CommissionAgentStats> Agents { get; set; } = new();
}
