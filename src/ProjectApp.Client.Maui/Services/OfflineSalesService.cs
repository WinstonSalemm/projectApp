using Microsoft.Extensions.Logging;
using ProjectApp.Client.Maui.Models;

namespace ProjectApp.Client.Maui.Services;

/// <summary>
/// Обертка для SalesService с поддержкой офлайн режима
/// </summary>
public class OfflineSalesService : ISalesService
{
    private readonly ISalesService _innerService;
    private readonly OfflineSyncService _syncService;
    private readonly AuthService _auth;
    private readonly ILogger<OfflineSalesService> _logger;

    public OfflineSalesService(
        ApiSalesService innerService,
        OfflineSyncService syncService,
        AuthService auth,
        ILogger<OfflineSalesService> logger)
    {
        _innerService = innerService;
        _syncService = syncService;
        _auth = auth;
        _logger = logger;
    }

    public async Task<SalesResult> SubmitSaleAsync(SaleDraft draft, CancellationToken ct = default)
    {
        var userName = _auth.UserName ?? "unknown";

        // Проверяем сеть
        if (!_syncService.IsOnline)
        {
            // Офлайн - сохраняем локально
            _logger.LogWarning("📴 Офлайн режим: продажа сохранена локально");
            
            var queued = await _syncService.QueueOperationAsync(
                OfflineOperationType.Sale,
                draft,
                userName);

            if (queued)
            {
                return SalesResult.Ok(null); // Возвращаем success но без ID (пока не синхронизировано)
            }
            else
            {
                return SalesResult.Fail("Не удалось сохранить операцию локально");
            }
        }

        // Онлайн - пытаемся отправить напрямую
        try
        {
            var result = await _innerService.SubmitSaleAsync(draft, ct);
            
            if (result.Success)
            {
                _logger.LogInformation("✅ Продажа отправлена напрямую (ID: {Id})", result.SaleId);
                return result;
            }
            else
            {
                // Не удалось отправить напрямую - сохраняем в очередь
                _logger.LogWarning("⚠️ Не удалось отправить напрямую, сохраняем в очередь: {Error}", result.ErrorMessage);
                
                await _syncService.QueueOperationAsync(
                    OfflineOperationType.Sale,
                    draft,
                    userName);

                return SalesResult.Ok(null); // Возвращаем success (операция в очереди)
            }
        }
        catch (Exception ex)
        {
            // Ошибка сети - сохраняем в очередь
            _logger.LogError(ex, "❌ Ошибка отправки, сохраняем в очередь");
            
            await _syncService.QueueOperationAsync(
                OfflineOperationType.Sale,
                draft,
                userName);

            return SalesResult.Ok(null); // Возвращаем success (операция в очереди)
        }
    }

    // Остальные методы делегируем напрямую
    public Task<bool> UploadSalePhotoAsync(int saleId, Stream photoStream, string fileName, CancellationToken ct = default)
        => _innerService.UploadSalePhotoAsync(saleId, photoStream, fileName, ct);
}
