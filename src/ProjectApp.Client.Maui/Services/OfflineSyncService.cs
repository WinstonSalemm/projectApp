using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Networking;
using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.Services;

/// <summary>
/// Сервис автоматической синхронизации офлайн операций
/// </summary>
public class OfflineSyncService
{
    private readonly LocalDatabase _localDb;
    private readonly ISalesService _salesService;
    private readonly ILogger<OfflineSyncService> _logger;
    private Timer? _syncTimer;
    private bool _isSyncing;

    // События для UI
    public event EventHandler<SyncStatusEventArgs>? SyncStatusChanged;
    public int PendingOperationsCount { get; private set; }
    public bool IsOnline { get; private set; }

    public OfflineSyncService(
        LocalDatabase localDb,
        ISalesService salesService,
        ILogger<OfflineSyncService> logger)
    {
        _localDb = localDb;
        _salesService = salesService;
        _logger = logger;

        // Подписываемся на изменения сети
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
        
        // Проверяем текущий статус сети
        IsOnline = Connectivity.NetworkAccess == NetworkAccess.Internet;
        
        // Запускаем инициализацию в фоне (не блокируем конструктор!)
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1000); // Даем UI время прогрузиться
                await UpdatePendingCountAsync();
                NotifyStatusChanged();
                
                // Запускаем периодическую проверку каждые 30 секунд
                _syncTimer = new Timer(async _ => await TrySyncAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка инициализации OfflineSyncService");
            }
        });
    }

    private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        var wasOnline = IsOnline;
        IsOnline = e.NetworkAccess == NetworkAccess.Internet;

        _logger.LogInformation("Connectivity changed: {Access}, Online: {Online}", e.NetworkAccess, IsOnline);

        if (!wasOnline && IsOnline)
        {
            // Интернет появился - синхронизируем!
            _logger.LogInformation("🌐 Интернет появился! Начинаем синхронизацию...");
            await TrySyncAsync();
        }

        NotifyStatusChanged();
    }

    /// <summary>
    /// Сохранить операцию локально и попытаться отправить
    /// </summary>
    public async Task<bool> QueueOperationAsync(string operationType, object payload, string createdBy)
    {
        try
        {
            var operation = new OfflineOperation
            {
                OperationType = operationType,
                PayloadJson = JsonSerializer.Serialize(payload),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy,
                Status = SyncStatus.Pending,
                RetryCount = 0
            };

            var id = await _localDb.SaveOperationAsync(operation);
            operation.Id = id;
            
            _logger.LogInformation("💾 Операция {Type} сохранена локально (ID: {Id})", operationType, id);

            await UpdatePendingCountAsync();
            NotifyStatusChanged();

            // Попытаемся отправить сразу
            if (IsOnline)
            {
                await TrySyncAsync();
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка сохранения операции {Type}", operationType);
            return false;
        }
    }

    /// <summary>
    /// Попытка синхронизации всех ожидающих операций
    /// </summary>
    public async Task TrySyncAsync()
    {
        if (_isSyncing || !IsOnline)
            return;

        try
        {
            _isSyncing = true;
            NotifyStatusChanged();

            var pending = await _localDb.GetPendingOperationsAsync();
            if (pending.Count == 0)
                return;

            _logger.LogInformation("🔄 Синхронизация: найдено {Count} операций", pending.Count);

            foreach (var operation in pending)
            {
                await SyncOperationAsync(operation);
            }

            await UpdatePendingCountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка синхронизации");
        }
        finally
        {
            _isSyncing = false;
            NotifyStatusChanged();
        }
    }

    private async Task SyncOperationAsync(OfflineOperation operation)
    {
        try
        {
            _logger.LogInformation("📤 Синхронизация операции {Type} (ID: {Id})", operation.OperationType, operation.Id);

            // Обновляем статус на "Syncing"
            await _localDb.UpdateOperationStatusAsync(operation.Id, SyncStatus.Syncing);

            bool success = false;
            int? serverRecordId = null;

            // Отправляем в зависимости от типа операции
            switch (operation.OperationType)
            {
                case OfflineOperationType.Sale:
                    var saleDraft = JsonSerializer.Deserialize<SaleDraft>(operation.PayloadJson);
                    if (saleDraft != null)
                    {
                        var result = await _salesService.SubmitSaleAsync(saleDraft);
                        success = result.Success;
                        serverRecordId = result.SaleId;
                        
                        if (!success)
                        {
                            await _localDb.UpdateOperationStatusAsync(
                                operation.Id, 
                                SyncStatus.Failed, 
                                result.ErrorMessage);
                            return;
                        }
                    }
                    break;

                // TODO: Добавить другие типы операций (Supply, Transfer, Defective, Refill)
                
                default:
                    _logger.LogWarning("⚠️ Неизвестный тип операции: {Type}", operation.OperationType);
                    break;
            }

            if (success)
            {
                await _localDb.UpdateOperationStatusAsync(
                    operation.Id, 
                    SyncStatus.Synced, 
                    null, 
                    serverRecordId);
                
                _logger.LogInformation("✅ Операция {Type} (ID: {Id}) успешно синхронизирована → Server ID: {ServerId}", 
                    operation.OperationType, operation.Id, serverRecordId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка синхронизации операции {Id}", operation.Id);
            await _localDb.UpdateOperationStatusAsync(
                operation.Id, 
                SyncStatus.Failed, 
                ex.Message);
        }
    }

    private async Task UpdatePendingCountAsync()
    {
        PendingOperationsCount = await _localDb.GetPendingCountAsync();
    }

    private void NotifyStatusChanged()
    {
        SyncStatusChanged?.Invoke(this, new SyncStatusEventArgs
        {
            IsOnline = IsOnline,
            IsSyncing = _isSyncing,
            PendingCount = PendingOperationsCount
        });
    }

    public void Dispose()
    {
        _syncTimer?.Dispose();
        Connectivity.ConnectivityChanged -= OnConnectivityChanged;
    }
}

public class SyncStatusEventArgs : EventArgs
{
    public bool IsOnline { get; set; }
    public bool IsSyncing { get; set; }
    public int PendingCount { get; set; }
}
