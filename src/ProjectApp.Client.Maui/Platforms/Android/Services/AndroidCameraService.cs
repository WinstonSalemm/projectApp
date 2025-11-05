using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using AndroidX.Core.Content;
using Java.IO;
using ProjectApp.Client.Maui.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ProjectApp.Client.Maui.Platforms.Android.Services;

/// <summary>
/// Android-специфичная реализация автоматической фотосъемки с фронтальной камеры
/// </summary>
public class AndroidCameraService : ICameraService
{
    private readonly Context _context;
    private CameraManager? _cameraManager;
    private string? _frontCameraId;

    public AndroidCameraService()
    {
        _context = Platform.CurrentActivity?.ApplicationContext 
            ?? throw new InvalidOperationException("Android Context not available");
    }

    public async Task<bool> IsCameraAvailableAsync()
    {
        try
        {
            _cameraManager = (CameraManager?)_context.GetSystemService(Context.CameraService);
            if (_cameraManager == null)
                return false;

            var cameraIds = _cameraManager.GetCameraIdList();
            if (cameraIds == null || cameraIds.Length == 0)
                return false;

            // Найти фронтальную камеру
            foreach (var id in cameraIds)
            {
                var characteristics = _cameraManager.GetCameraCharacteristics(id);
                var facing = (int?)characteristics?.Get(CameraCharacteristics.LensFacing);
                
                if (facing == (int)LensFacing.Front)
                {
                    _frontCameraId = id;
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidCameraService] IsCameraAvailableAsync error: {ex}");
            return false;
        }
    }

    public async Task<string?> TakeSilentPhotoAsync()
    {
        try
        {
            // Проверяем доступность камеры
            if (!await IsCameraAvailableAsync() || string.IsNullOrEmpty(_frontCameraId))
            {
                System.Diagnostics.Debug.WriteLine("[AndroidCameraService] Front camera not available");
                return null;
            }

            // Создаем временный файл для фото
            var photoFile = new Java.IO.File(
                _context.CacheDir,
                $"security_photo_{DateTime.Now:yyyyMMdd_HHmmss}.jpg"
            );

            // Открываем камеру и делаем фото
            var photoPath = await CapturePhotoAsync(photoFile);
            
            System.Diagnostics.Debug.WriteLine($"[AndroidCameraService] Photo captured: {photoPath}");
            return photoPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidCameraService] TakeSilentPhotoAsync error: {ex}");
            return null;
        }
    }

    private Task<string?> CapturePhotoAsync(Java.IO.File outputFile)
    {
        var tcs = new TaskCompletionSource<string?>();

        try
        {
            if (_cameraManager == null || string.IsNullOrEmpty(_frontCameraId))
            {
                tcs.SetResult(null);
                return tcs.Task;
            }

            // Открываем камеру
            _cameraManager.OpenCamera(_frontCameraId, new CameraStateCallback(
                onOpened: async (camera) =>
                {
                    try
                    {
                        // Получаем характеристики камеры
                        var characteristics = _cameraManager.GetCameraCharacteristics(_frontCameraId);
                        var map = (StreamConfigurationMap?)characteristics?.Get(CameraCharacteristics.ScalerStreamConfigurationMap);
                        
                        if (map == null)
                        {
                            camera.Close();
                            tcs.SetResult(null);
                            return;
                        }

                        // Выбираем оптимальный размер фото (средний, не максимальный для скорости)
                        var sizes = map.GetOutputSizes((int)ImageFormatType.Jpeg);
                        var optimalSize = sizes?.OrderBy(s => s.Width * s.Height)
                            .Skip(sizes.Length / 2)
                            .FirstOrDefault() ?? new global::Android.Util.Size(640, 480);

                        // Создаем ImageReader для получения фото
                        var reader = ImageReader.NewInstance(optimalSize.Width, optimalSize.Height, 
                            ImageFormatType.Jpeg, 1);

                        var readerSurface = reader.Surface;
                        if (readerSurface == null)
                        {
                            camera.Close();
                            tcs.SetResult(null);
                            return;
                        }

                        // Обработчик когда фото готово
                        reader.SetOnImageAvailableListener(new ImageAvailableListener(
                            onImageAvailable: (imgReader) =>
                            {
                                try
                                {
                                    using var image = imgReader.AcquireLatestImage();
                                    if (image == null)
                                    {
                                        camera.Close();
                                        tcs.SetResult(null);
                                        return;
                                    }

                                    // Сохраняем фото
                                    var buffer = image.GetPlanes()?[0]?.Buffer;
                                    if (buffer == null)
                                    {
                                        camera.Close();
                                        tcs.SetResult(null);
                                        return;
                                    }

                                    var bytes = new byte[buffer.Remaining()];
                                    buffer.Get(bytes);

                                    using var output = new FileOutputStream(outputFile);
                                    output.Write(bytes);
                                    output.Flush();

                                    camera.Close();
                                    tcs.SetResult(outputFile.AbsolutePath);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[AndroidCameraService] Image save error: {ex}");
                                    camera.Close();
                                    tcs.SetResult(null);
                                }
                            }
                        ), null);

                        // Создаем capture request
                        var captureRequest = camera.CreateCaptureRequest(CameraTemplate.StillCapture);
                        captureRequest?.AddTarget(readerSurface);
                        captureRequest?.Set(CaptureRequest.JpegQuality, (Java.Lang.Byte)(sbyte)85); // Качество 85%
                        
                        // Создаем capture session
                        camera.CreateCaptureSession(
                            new[] { readerSurface },
                            new CameraSessionStateCallback(
                                onConfigured: (session) =>
                                {
                                    try
                                    {
                                        // Делаем фото
                                        session.Capture(captureRequest?.Build(), 
                                            new CameraCaptureCallback(), null);
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[AndroidCameraService] Capture error: {ex}");
                                        camera.Close();
                                        tcs.SetResult(null);
                                    }
                                },
                                onConfigureFailed: (session) =>
                                {
                                    camera.Close();
                                    tcs.SetResult(null);
                                }
                            ),
                            null
                        );
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AndroidCameraService] Camera session error: {ex}");
                        camera.Close();
                        tcs.SetResult(null);
                    }
                },
                onError: (camera, error) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[AndroidCameraService] Camera error: {error}");
                    camera?.Close();
                    tcs.SetResult(null);
                }
            ), null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidCameraService] CapturePhotoAsync error: {ex}");
            tcs.SetResult(null);
        }

        return tcs.Task;
    }

    public async Task<byte[]?> GetPhotoBytes(string photoPath)
    {
        try
        {
            if (string.IsNullOrEmpty(photoPath) || !System.IO.File.Exists(photoPath))
                return null;

            return await System.IO.File.ReadAllBytesAsync(photoPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AndroidCameraService] GetPhotoBytes error: {ex}");
            return null;
        }
    }
}

// Callback классы для Camera2 API

internal class CameraStateCallback : CameraDevice.StateCallback
{
    private readonly Action<CameraDevice> _onOpened;
    private readonly Action<CameraDevice, CameraError> _onError;

    public CameraStateCallback(Action<CameraDevice> onOpened, Action<CameraDevice, CameraError> onError)
    {
        _onOpened = onOpened;
        _onError = onError;
    }

    public override void OnOpened(CameraDevice camera)
    {
        _onOpened?.Invoke(camera);
    }

    public override void OnError(CameraDevice camera, [GeneratedEnum] CameraError error)
    {
        _onError?.Invoke(camera, error);
    }

    public override void OnDisconnected(CameraDevice camera)
    {
        camera.Close();
    }
}

internal class CameraSessionStateCallback : CameraCaptureSession.StateCallback
{
    private readonly Action<CameraCaptureSession> _onConfigured;
    private readonly Action<CameraCaptureSession> _onConfigureFailed;

    public CameraSessionStateCallback(
        Action<CameraCaptureSession> onConfigured,
        Action<CameraCaptureSession> onConfigureFailed)
    {
        _onConfigured = onConfigured;
        _onConfigureFailed = onConfigureFailed;
    }

    public override void OnConfigured(CameraCaptureSession session)
    {
        _onConfigured?.Invoke(session);
    }

    public override void OnConfigureFailed(CameraCaptureSession session)
    {
        _onConfigureFailed?.Invoke(session);
    }
}

internal class CameraCaptureCallback : CameraCaptureSession.CaptureCallback
{
    // Пустой callback, нам не нужны события capture
}

internal class ImageAvailableListener : Java.Lang.Object, ImageReader.IOnImageAvailableListener
{
    private readonly Action<ImageReader> _onImageAvailable;

    public ImageAvailableListener(Action<ImageReader> onImageAvailable)
    {
        _onImageAvailable = onImageAvailable;
    }

    public void OnImageAvailable(ImageReader? reader)
    {
        if (reader != null)
            _onImageAvailable?.Invoke(reader);
    }
}
