using System.Windows;
using System.Windows.Media.Imaging;
using ScreenshotTool.App.Views;
using ScreenshotTool.Core.Capture;
using ScreenshotTool.Core.Clipboard;
using ScreenshotTool.Core.Infrastructure;
using ScreenshotTool.Core.Storage;
using ScreenshotTool.Core.Annotation;
using ScreenshotTool.Core.Ocr;
using ScreenshotTool.Core.Translation;

namespace ScreenshotTool.App.Services;

public sealed class CaptureCoordinator
{
    private readonly ICaptureService _captureService;
    private readonly IAppLogger _logger;
    private readonly TrayIconService _trayIcon;
    private readonly IClipboardService _clipboardService;
    private readonly IImageSaveService _imageSaveService;
    private readonly PinManager _pinManager;
    private readonly IOcrService _ocrService;
    private readonly ITranslationService _translationService;
    private readonly List<CaptureOverlayWindow> _activeOverlays = new();
    private TaskCompletionSource<CaptureCompletedEventArgs?>? _activeCompletion;
    private bool _isCapturing;

    public CaptureCoordinator(ICaptureService captureService, IAppLogger logger, TrayIconService trayIcon, IClipboardService clipboardService, IImageSaveService imageSaveService, PinManager pinManager, IOcrService ocrService, ITranslationService translationService)
    {
        _captureService = captureService;
        _logger = logger;
        _trayIcon = trayIcon;
        _clipboardService = clipboardService;
        _imageSaveService = imageSaveService;
        _pinManager = pinManager;
        _ocrService = ocrService;
        _translationService = translationService;
    }

    public async Task StartCaptureAsync()
    {
        if (_isCapturing)
        {
            return;
        }

        _isCapturing = true;
        try
        {
            var virtualBounds = _captureService.GetVirtualScreenBounds();
            var desktop = _captureService.CaptureVirtualScreen();
            var completion = new TaskCompletionSource<CaptureCompletedEventArgs?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _activeCompletion = completion;

            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                var screenBounds = new VirtualScreenBounds(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height);
                var frozenScreen = CreateScreenImage(desktop, virtualBounds, screenBounds);
                var overlay = new CaptureOverlayWindow(frozenScreen, screenBounds);
                overlay.CaptureCompleted += (_, eventArgs) => completion.TrySetResult(eventArgs);
                overlay.CaptureCancelled += (_, _) => completion.TrySetResult(null);
                _activeOverlays.Add(overlay);
                overlay.Show();
            }

            var result = await completion.Task;
            BitmapSource? selectedImage = null;
            if (result is not null)
            {
                selectedImage = CreateSelectedImage(desktop, virtualBounds, result.Region);
                if (result.Action == CaptureAction.Pin)
                {
                    _pinManager.Create(selectedImage, result.Region);
                }
            }

            foreach (var overlay in _activeOverlays)
            {
                overlay.Close();
            }

            _activeOverlays.Clear();
            if (result is not null && result.Action != CaptureAction.Pin && selectedImage is not null)
            {
                await ProcessActionAsync(result.Action, selectedImage, result.Region);
            }
        }
        catch (Exception exception)
        {
            _logger.Error("截图失败。", exception);
            System.Windows.MessageBox.Show("截图失败，请重试。", "周天截图", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            foreach (var overlay in _activeOverlays)
            {
                overlay.Close();
            }

            _activeOverlays.Clear();
            _activeCompletion = null;
            _isCapturing = false;
        }
    }

    public bool HandlePinHotkey()
    {
        if (!_isCapturing)
        {
            return false;
        }

        var region = _activeOverlays
            .Select(overlay => overlay.SelectedRegion)
            .FirstOrDefault(selection => selection is not null);
        if (region is not null)
        {
            _activeCompletion?.TrySetResult(new CaptureCompletedEventArgs(CaptureAction.Pin, region.Value));
        }

        return true;
    }

    public void CancelActiveCapture() => _activeCompletion?.TrySetResult(null);

    private async Task ProcessActionAsync(CaptureAction action, BitmapSource image, CaptureRegion region)
    {
        if (action == CaptureAction.Copy)
        {
            await _clipboardService.SetImageAsync(image);
            _logger.Information($"截图已复制：Width={region.Width}, Height={region.Height}。");
            _trayIcon.ShowInformation($"已复制 {region.Width} × {region.Height} 截图到剪贴板。");
            return;
        }

        if (action == CaptureAction.Save)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG 图片 (*.png)|*.png|JPG 图片 (*.jpg)|*.jpg",
                DefaultExt = ".png",
                AddExtension = true,
                FileName = $"Screenshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png"
            };
            if (dialog.ShowDialog() == true)
            {
                await _imageSaveService.SaveAsync(image, dialog.FileName);
                _logger.Information($"截图已保存：{dialog.FileName}");
                _trayIcon.ShowInformation("截图已保存。");
            }
        }
        else if (action == CaptureAction.Pin)
        {
            _pinManager.Create(image, region);
        }
        else if (action == CaptureAction.Translate)
        {
            _pinManager.CreateAndTranslate(image, region);
        }
        else if (action == CaptureAction.Ocr)
        {
            try
            {
                var result = await _ocrService.RecognizeAsync(image);
                new OcrResultWindow(result.Text).Show();
            }
            catch (Exception exception)
            {
                _logger.Error("截图 OCR 失败。", exception);
                System.Windows.MessageBox.Show("文字提取失败：" + exception.Message, "周天截图", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private static BitmapSource CreateScreenImage(BitmapSource desktop, VirtualScreenBounds virtualBounds, VirtualScreenBounds screenBounds)
    {
        var image = new CroppedBitmap(desktop, new Int32Rect(
            screenBounds.X - virtualBounds.X,
            screenBounds.Y - virtualBounds.Y,
            screenBounds.Width,
            screenBounds.Height));
        image.Freeze();
        return image;
    }

    private static BitmapSource CreateSelectedImage(BitmapSource desktop, VirtualScreenBounds bounds, CaptureRegion region)
    {
        var safe = region.ClampTo(bounds);
        var x = Math.Clamp(safe.X - bounds.X, 0, Math.Max(0, desktop.PixelWidth - 1));
        var y = Math.Clamp(safe.Y - bounds.Y, 0, Math.Max(0, desktop.PixelHeight - 1));
        var width = Math.Clamp(safe.Width, 1, desktop.PixelWidth - x);
        var height = Math.Clamp(safe.Height, 1, desktop.PixelHeight - y);
        var image = new CroppedBitmap(desktop, new Int32Rect(x, y, width, height));
        var detached = new WriteableBitmap(image);
        detached.Freeze();
        return detached;
    }
}
