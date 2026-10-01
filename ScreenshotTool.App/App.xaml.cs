using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ScreenshotTool.App.Services;
using ScreenshotTool.Core.Capture;
using ScreenshotTool.Core.Hotkeys;
using ScreenshotTool.Core.Infrastructure;
using ScreenshotTool.Core.Storage;
using ScreenshotTool.Core.Annotation;
using ScreenshotTool.Core.Ocr;
using ScreenshotTool.Core.Security;
using ScreenshotTool.Core.Translation;
using ScreenshotTool.App.Views;

using ScreenshotTool.Core.Clipboard;

namespace ScreenshotTool.App;

// VioletSnap，由作者周天开发与维护。
public partial class App : System.Windows.Application
{
    private static readonly Mutex InstanceMutex;
    private static readonly bool IsFirstInstance;
    private ServiceRegistry? _services;

    static App()
    {
        InstanceMutex = new Mutex(initiallyOwned: true, "Local\\ScreenshotTool.SingleInstance", out var createdNew);
        IsFirstInstance = createdNew;
    }

    private void OnStartup(object sender, StartupEventArgs eventArgs)
    {
        if (!IsFirstInstance)
        {
            System.Windows.MessageBox.Show("VioletSnap 已在运行，请在系统托盘中使用现有实例。", "VioletSnap", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        if (eventArgs.Args.Contains("--install-startup-task", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                new StartupRegistrationService().SetEnabled(true);
                Shutdown();
            }
            catch
            {
                Shutdown(-1);
            }
            return;
        }

        try
        {
            _services = CreateServices();
            var settings = _services.GetRequiredService<ISettingsService>();
            settings.Load();

            var logger = _services.GetRequiredService<IAppLogger>();
            logger.Information("应用程序启动。");

            var trayIcon = _services.GetRequiredService<TrayIconService>();
            trayIcon.ExitRequested += OnExitRequested;
            trayIcon.ScreenshotRequested += OnScreenshotRequested;
            trayIcon.PinRequested += OnPinRequested;
            trayIcon.SettingsRequested += OnSettingsRequested;
            trayIcon.RestorePinRequested += (_, _) => _services.GetRequiredService<PinManager>().RestoreLastClosed();
            trayIcon.TogglePinsRequested += (_, _) => _services.GetRequiredService<PinManager>().ToggleAll();
            trayIcon.ClosePinsRequested += (_, _) => _services.GetRequiredService<PinManager>().CloseAll();
            trayIcon.Start();

            var hotkeys = _services.GetRequiredService<IHotkeyManager>();
            hotkeys.ScreenshotRequested += OnScreenshotRequested;
            hotkeys.PinRequested += OnPinRequested;
            var hotkeyResult = hotkeys.Register(settings.Current.Hotkeys.Screenshot, settings.Current.Hotkeys.Pin);
            if (!hotkeyResult.Success)
            {
                var message = string.Join(Environment.NewLine, hotkeyResult.Errors) + Environment.NewLine + "程序已正常启动，可通过托盘菜单打开设置并更换快捷键。";
                logger.Information(message);
                trayIcon.ShowInformation(message);
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
            _services?.GetRequiredService<IAppLogger>().Error("应用程序初始化失败。", exception);
            System.Windows.MessageBox.Show("程序初始化失败，请查看日志后重试。", "VioletSnap", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private void OnExit(object sender, ExitEventArgs eventArgs)
    {
        if (_services is null)
        {
            return;
        }

        _services.GetRequiredService<IAppLogger>().Information("应用程序退出。");
        _services.Dispose();
        _services = null;
        InstanceMutex.ReleaseMutex();
    }

    private void OnExitRequested(object? sender, EventArgs eventArgs)
    {
        Shutdown();
    }

    private void OnScreenshotRequested(object? sender, EventArgs eventArgs)
    {
        _ = _services?.GetRequiredService<CaptureCoordinator>().StartCaptureAsync();
    }

    private void OnPinRequested(object? sender, EventArgs eventArgs)
    {
        if (_services is null)
        {
            return;
        }

        if (!_services.GetRequiredService<CaptureCoordinator>().HandlePinHotkey())
        {
            _services.GetRequiredService<PinManager>().CreateFromClipboard();
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        try
        {
            _services?.GetRequiredService<IAppLogger>().Error("界面操作发生未处理异常。", eventArgs.Exception);
            _services?.GetRequiredService<CaptureCoordinator>().CancelActiveCapture();
        }
        catch
        {
            // 异常保护逻辑本身不能导致常驻程序退出。
        }

        eventArgs.Handled = true;
        System.Windows.MessageBox.Show(
            "本次操作未能完成，截图状态已安全退出。程序仍会继续运行，请重试。",
            "VioletSnap",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void OnSettingsRequested(object? sender, EventArgs eventArgs)
    {
        if (_services is null) return;
        new SettingsWindow(
            _services.GetRequiredService<ISettingsService>(),
            _services.GetRequiredService<ISecretStorageService>(),
            _services.GetRequiredService<ITranslationModelService>(),
            _services.GetRequiredService<IHotkeyManager>()).ShowDialog();
    }

    private static ServiceRegistry CreateServices()
    {
        var applicationDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScreenshotTool"); // 保留旧数据目录名，升级后继续读取用户设置、加密密钥与日志。
        var services = new ServiceRegistry();
        services.AddSingleton<IAppLogger>(_ => new FileAppLogger(applicationDataDirectory));
        services.AddSingleton<ISettingsService>(provider => new SettingsService(
            applicationDataDirectory,
            provider.GetRequiredService<IAppLogger>()));
        services.AddSingleton<IStartupRegistrationService>(_ => new StartupRegistrationService());
        services.AddSingleton(provider => new TrayIconService(
            provider.GetRequiredService<IAppLogger>(),
            provider.GetRequiredService<IStartupRegistrationService>()));
        services.AddSingleton<ICaptureService>(_ => new GdiCaptureService());
        services.AddSingleton<IHotkeyManager>(_ => new HotkeyManager());
        services.AddSingleton<IClipboardService>(_ => new ClipboardService());
        services.AddSingleton<IImageSaveService>(_ => new ImageSaveService());
        services.AddSingleton<IAnnotationRenderer>(_ => new AnnotationRenderer());
        services.AddSingleton<ISecretStorageService>(_ => new SecretStorageService());
        services.AddSingleton<ITranslationModelService>(_ => new TranslationModelService());
        services.AddSingleton<IOcrService>(provider => new WindowsOcrService(provider.GetRequiredService<ISettingsService>()));
        services.AddSingleton<ITranslationService>(provider => new OpenAiCompatibleTranslationService(
            provider.GetRequiredService<ISettingsService>(),
            provider.GetRequiredService<ISecretStorageService>()));
        services.AddSingleton(provider => new PinManager(
            provider.GetRequiredService<IClipboardService>(),
            provider.GetRequiredService<IImageSaveService>(),
            provider.GetRequiredService<IAppLogger>(),
            provider.GetRequiredService<TrayIconService>(),
            provider.GetRequiredService<IAnnotationRenderer>(),
            provider.GetRequiredService<IOcrService>(),
            provider.GetRequiredService<ITranslationService>()));
        services.AddSingleton(provider => new CaptureCoordinator(
            provider.GetRequiredService<ICaptureService>(),
            provider.GetRequiredService<IAppLogger>(),
            provider.GetRequiredService<TrayIconService>(),
            provider.GetRequiredService<IClipboardService>(),
            provider.GetRequiredService<IImageSaveService>(),
            provider.GetRequiredService<PinManager>(),
            provider.GetRequiredService<IOcrService>(),
            provider.GetRequiredService<ITranslationService>()));
        return services;
    }
}
