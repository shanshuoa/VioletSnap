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

public sealed class PinManager : IDisposable
{
    private readonly List<PinWindow> _windows = new();
    private readonly Dictionary<PinWindow, System.Threading.CancellationTokenSource> _translations = new();
    private readonly HashSet<PinWindow> _recognizing = new();
    private BitmapSource? _lastClosedImage;
    private Action<PinWindow>? _restoreLast;
    private bool _disposing;

    public void RestoreLastClosed()
    {
        if (_lastClosedImage is not { } image) { _trayIcon.ShowInformation("没有可恢复的贴图。"); return; }
        _lastClosedImage = null;
        var restore = _restoreLast;
        _restoreLast = null;
        restore?.Invoke(Create(image));
    }

    public void ToggleAll()
    {
        var hide = _windows.Any(window => window.IsVisible);
        foreach (var window in _windows)
            if (hide) window.Hide(); else window.Show();
    }
    private readonly IClipboardService _clipboardService;
    private readonly IImageSaveService _imageSaveService;
    private readonly IAppLogger _logger;
    private readonly TrayIconService _trayIcon;
    private readonly IAnnotationRenderer _annotationRenderer;
    private readonly IOcrService _ocrService;
    private readonly ITranslationService _translationService;

    public PinManager(IClipboardService clipboardService, IImageSaveService imageSaveService, IAppLogger logger, TrayIconService trayIcon, IAnnotationRenderer annotationRenderer, IOcrService ocrService, ITranslationService translationService)
    {
        _clipboardService = clipboardService;
        _imageSaveService = imageSaveService;
        _logger = logger;
        _trayIcon = trayIcon;
        _annotationRenderer = annotationRenderer;
        _ocrService = ocrService;
        _translationService = translationService;
    }

    public void CreateFromClipboard()
    {
        try
        {
            if (!_clipboardService.ContainsImage() || _clipboardService.GetImage() is not { } image)
            {
                _trayIcon.ShowInformation("剪贴板中没有可贴出的图片。");
                return;
            }

            Create(image);
        }
        catch (Exception exception)
        {
            _logger.Error("读取剪贴板贴图失败。", exception);
            System.Windows.MessageBox.Show("读取剪贴板图片失败，请重试。", "周天截图", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public PinWindow Create(BitmapSource image, CaptureRegion? initialRegion = null)
    {
        var window = new PinWindow(image, _annotationRenderer, initialRegion, initialAnnotationMode: false);
        window.CopyRequested += OnCopyRequested;
        window.SaveRequested += OnSaveRequested;
        window.OcrRequested += OnOcrRequested;
        window.TranslateRequested += OnTranslateRequested;
        window.CancelTranslationRequested += OnCancelTranslationRequested;
        window.Closed += OnWindowClosed;
        _windows.Add(window);
        window.Show();
        _logger.Information($"已创建贴图：Width={image.PixelWidth}, Height={image.PixelHeight}。");
        return window;
    }

    public PinWindow CreateAndTranslate(BitmapSource image, CaptureRegion? initialRegion = null)
    {
        var window = Create(image, initialRegion);
        _ = TranslateInPlaceAsync(window);
        return window;
    }

    public void CloseAll()
    {
        foreach (var window in _windows.ToArray())
        {
            window.Close();
        }
    }

    public void Dispose() { _disposing = true; CloseAll(); _lastClosedImage = null; _restoreLast = null; }

    private async void OnCopyRequested(object? sender, EventArgs eventArgs)
    {
        if (sender is not PinWindow window)
        {
            return;
        }

        try
        {
            await _clipboardService.SetImageAsync(window.RenderedImage);
            _trayIcon.ShowInformation("贴图已复制到剪贴板。");
        }
        catch (Exception exception)
        {
            _logger.Error("复制贴图失败。", exception);
            _trayIcon.ShowInformation("复制失败，剪贴板可能被占用，请重试。");
        }
    }

    private async void OnSaveRequested(object? sender, EventArgs eventArgs)
    {
        if (sender is not PinWindow window)
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG 图片 (*.png)|*.png|JPG 图片 (*.jpg)|*.jpg",
            DefaultExt = ".png",
            AddExtension = true,
            FileName = $"Screenshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await _imageSaveService.SaveAsync(window.RenderedImage, dialog.FileName);
            _trayIcon.ShowInformation("贴图已保存。");
        }
        catch (Exception exception)
        {
            _logger.Error("保存贴图失败。", exception);
            System.Windows.MessageBox.Show("保存图片失败，请重试。", "周天截图", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnWindowClosed(object? sender, EventArgs eventArgs)
    {
        if (sender is PinWindow window)
        {
            if (!_disposing) { _lastClosedImage = window.Image; _restoreLast = window.CaptureRestoreState(); }
            if (_translations.Remove(window, out var cancellation)) cancellation.Cancel();
            window.CancelTranslationRequested -= OnCancelTranslationRequested;
            window.CopyRequested -= OnCopyRequested;
            window.SaveRequested -= OnSaveRequested;
            window.OcrRequested -= OnOcrRequested;
            window.TranslateRequested -= OnTranslateRequested;
            window.Closed -= OnWindowClosed;
            _windows.Remove(window);
        }
    }

    private async void OnOcrRequested(object? sender, EventArgs eventArgs) => await RecognizeAsync(sender as PinWindow);
    private async void OnTranslateRequested(object? sender, EventArgs eventArgs)
    {
        if (sender is PinWindow window) await TranslateInPlaceAsync(window);
    }

    private async Task RecognizeAsync(PinWindow? window)
    {
        if (window is null || !_recognizing.Add(window)) return;
        try
        {
            _trayIcon.ShowInformation("正在识别文字…");
            var result = await _ocrService.RecognizeAsync(window.RecognitionImage);
            if (!_windows.Contains(window)) return;
            new OcrResultWindow(result.Text).Show();
        }
        catch (Exception exception)
        {
            _logger.Error("OCR 识别失败。", exception);
            System.Windows.MessageBox.Show("文字提取失败：" + exception.Message, "周天截图", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { _recognizing.Remove(window); }
    }

    private void OnCancelTranslationRequested(object? sender, EventArgs e)
    {
        if (sender is PinWindow window && _translations.Remove(window, out var cancellation))
        {
            cancellation.Cancel();
            window.ShowTranslationError("已取消，可点击翻译重试");
        }
    }

    private async Task TranslateInPlaceAsync(PinWindow window)
    {
        if (_translations.ContainsKey(window)) return;
        using var cancellation = new System.Threading.CancellationTokenSource();
        _translations[window] = cancellation;
        window.ShowTranslationLoading();
        try
        {
            var ocrResult = await _ocrService.RecognizeAsync(window.RecognitionImage, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(ocrResult.Text))
            {
                window.ShowTranslationError("未识别到可翻译的文字");
                return;
            }

            var result = await _translationService.TranslateAsync(new TranslationRequest
            {
                Text = ocrResult.Text
            }, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (!_windows.Contains(window)) return;
            window.ShowTranslationResult(ocrResult, result.Text);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (OperationCanceledException)
        {
            if (_windows.Contains(window)) window.ShowTranslationError("翻译超时，请检查网络后重试");
        }
        catch (Exception exception)
        {
            _logger.Error("贴图内翻译失败。", exception);
            if (!cancellation.IsCancellationRequested && _windows.Contains(window)) window.ShowTranslationError(exception.Message);
        }
        finally
        {
            if (_translations.TryGetValue(window, out var active) && ReferenceEquals(active, cancellation))
                _translations.Remove(window);
        }
    }

}
