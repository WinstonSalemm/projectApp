using System;
using System.IO;
using System.Threading.Tasks;

namespace ProjectApp.Client.Maui.Services;

/// <summary>
/// Сервис для автоматической фотосъемки при операциях продажи
/// </summary>
public class SalePhotoService
{
    private readonly ICameraService _camera;
    private readonly ISalesService _sales;

    public SalePhotoService(ICameraService camera, ISalesService sales)
    {
        _camera = camera;
        _sales = sales;
    }

    /// <summary>
    /// Автоматически сделать фото и загрузить к продаже
    /// </summary>
    /// <param name="saleId">ID созданной продажи</param>
    /// <param name="operationType">Тип операции (для логирования)</param>
    public async Task TakeAndUploadPhotoAsync(int saleId, string operationType = "Sale")
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[SalePhotoService] Taking automatic photo for {operationType} #{saleId}");

            // Проверяем доступность камеры
            var isCameraAvailable = await _camera.IsCameraAvailableAsync();
            if (!isCameraAvailable)
            {
                System.Diagnostics.Debug.WriteLine("[SalePhotoService] Camera not available, skipping photo");
                return;
            }

            // Делаем фото автоматически БЕЗ UI
            var photoPath = await _camera.TakeSilentPhotoAsync();
            if (string.IsNullOrEmpty(photoPath))
            {
                System.Diagnostics.Debug.WriteLine("[SalePhotoService] Failed to capture photo");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[SalePhotoService] Photo captured: {photoPath}");

            // Загружаем фото на сервер
            try
            {
                using var photoStream = File.OpenRead(photoPath);
                var fileName = $"security_photo_{saleId}_{DateTime.Now:yyyyMMddHHmmss}.jpg";
                
                var uploaded = await _sales.UploadSalePhotoAsync(saleId, photoStream, fileName);
                
                if (uploaded)
                {
                    System.Diagnostics.Debug.WriteLine($"[SalePhotoService] Photo uploaded successfully for sale #{saleId}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[SalePhotoService] Photo upload failed for sale #{saleId}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SalePhotoService] Upload error: {ex.Message}");
            }
            finally
            {
                // Удаляем локальное фото после загрузки
                try
                {
                    if (File.Exists(photoPath))
                        File.Delete(photoPath);
                }
                catch
                {
                    // Игнорируем ошибки удаления
                }
            }
        }
        catch (Exception ex)
        {
            // Не блокируем процесс продажи если фото не удалось
            System.Diagnostics.Debug.WriteLine($"[SalePhotoService] Error taking/uploading photo: {ex}");
        }
    }

    /// <summary>
    /// Получить байты фото для отправки в Telegram
    /// </summary>
    public async Task<byte[]?> GetPhotoForTelegramAsync(int saleId)
    {
        try
        {
            // Делаем фото
            var photoPath = await _camera.TakeSilentPhotoAsync();
            if (string.IsNullOrEmpty(photoPath))
                return null;

            // Получаем байты
            var bytes = await _camera.GetPhotoBytes(photoPath);

            // Удаляем локальное фото
            try
            {
                if (File.Exists(photoPath))
                    File.Delete(photoPath);
            }
            catch
            {
                // Игнорируем ошибки удаления
            }

            return bytes;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SalePhotoService] GetPhotoForTelegramAsync error: {ex}");
            return null;
        }
    }
}
