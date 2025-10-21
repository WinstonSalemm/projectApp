using SQLite;
using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.Services;

/// <summary>
/// Локальная SQLite база данных для офлайн режима
/// </summary>
public class LocalDatabase
{
    private readonly SQLiteAsyncConnection _db;
    private bool _initialized;

    public LocalDatabase()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "pojpro_offline.db3");
        _db = new SQLiteAsyncConnection(dbPath);
    }

    private async Task InitializeAsync()
    {
        if (_initialized) return;
        
        await _db.CreateTableAsync<OfflineOperation>();
        _initialized = true;
    }

    #region Offline Operations

    /// <summary>
    /// Сохранить операцию для синхронизации
    /// </summary>
    public async Task<int> SaveOperationAsync(OfflineOperation operation)
    {
        await InitializeAsync();
        return await _db.InsertAsync(operation);
    }

    /// <summary>
    /// Получить все операции ожидающие синхронизации
    /// </summary>
    public async Task<List<OfflineOperation>> GetPendingOperationsAsync()
    {
        await InitializeAsync();
        return await _db.Table<OfflineOperation>()
            .Where(o => o.Status == SyncStatus.Pending || o.Status == SyncStatus.Failed)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Обновить статус операции
    /// </summary>
    public async Task UpdateOperationStatusAsync(int id, string status, string? error = null, int? serverRecordId = null)
    {
        await InitializeAsync();
        var op = await _db.Table<OfflineOperation>()
            .Where(o => o.Id == id)
            .FirstOrDefaultAsync();
        
        if (op != null)
        {
            op.Status = status;
            op.LastSyncAttempt = DateTime.UtcNow;
            op.LastError = error;
            op.ServerRecordId = serverRecordId;
            op.RetryCount++;
            await _db.UpdateAsync(op);
        }
    }

    /// <summary>
    /// Получить количество несинхронизированных операций
    /// </summary>
    public async Task<int> GetPendingCountAsync()
    {
        await InitializeAsync();
        return await _db.Table<OfflineOperation>()
            .Where(o => o.Status == SyncStatus.Pending || o.Status == SyncStatus.Failed)
            .CountAsync();
    }

    /// <summary>
    /// Удалить успешно синхронизированные операции старше N дней
    /// </summary>
    public async Task CleanupOldOperationsAsync(int daysToKeep = 7)
    {
        await InitializeAsync();
        var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
        await _db.ExecuteAsync(
            "DELETE FROM OfflineOperations WHERE Status = ? AND CreatedAt < ?",
            SyncStatus.Synced, cutoffDate);
    }

    /// <summary>
    /// Получить все операции (для отладки/истории)
    /// </summary>
    public async Task<List<OfflineOperation>> GetAllOperationsAsync()
    {
        await InitializeAsync();
        return await _db.Table<OfflineOperation>()
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    #endregion
}
