using System.Threading.Tasks;

namespace ProjectApp.Client.Maui.Services;

/// <summary>
/// Сервис для автоматической фотосъемки с фронтальной камеры
/// </summary>
public interface ICameraService
{
    /// <summary>
    /// Проверить доступность камеры
    /// </summary>
    Task<bool> IsCameraAvailableAsync();

    /// <summary>
    /// Автоматически сделать фото с фронтальной камеры БЕЗ UI
    /// </summary>
    /// <returns>Путь к сохраненному фото или null если ошибка</returns>
    Task<string?> TakeSilentPhotoAsync();

    /// <summary>
    /// Получить байты фото по пути
    /// </summary>
    Task<byte[]?> GetPhotoBytes(string photoPath);
}
