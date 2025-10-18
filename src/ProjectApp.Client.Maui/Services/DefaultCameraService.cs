using System.Threading.Tasks;

namespace ProjectApp.Client.Maui.Services;

/// <summary>
/// Default implementation for non-Android platforms
/// </summary>
public class DefaultCameraService : ICameraService
{
    public Task<bool> IsCameraAvailableAsync()
    {
        System.Diagnostics.Debug.WriteLine("[DefaultCameraService] Camera service not available on this platform");
        return Task.FromResult(false);
    }

    public Task<string?> TakeSilentPhotoAsync()
    {
        System.Diagnostics.Debug.WriteLine("[DefaultCameraService] Silent photo not supported on this platform");
        return Task.FromResult<string?>(null);
    }

    public Task<byte[]?> GetPhotoBytes(string photoPath)
    {
        return Task.FromResult<byte[]?>(null);
    }
}
