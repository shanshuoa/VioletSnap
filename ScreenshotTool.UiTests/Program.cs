using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using ScreenshotTool.App.Views;
using ScreenshotTool.App.Services;
using ScreenshotTool.App;
using ScreenshotTool.Core.Annotation;
using ScreenshotTool.Core.Configuration;
using ScreenshotTool.Core.Hotkeys;
using ScreenshotTool.Core.Infrastructure;
using ScreenshotTool.Core.Ocr;
using ScreenshotTool.Core.Security;
using ScreenshotTool.Core.Translation;
using ScreenshotTool.Core.Capture;
using ScreenshotTool.Core.Clipboard;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var xml = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Theme.xaml"));
            XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            var dictionary = new XElement(ns + "ResourceDictionary", new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"), xml.Root!.Element(ns + "Application.Resources")!.Elements());
            application.Resources = (ResourceDictionary)XamlReader.Parse(dictionary.ToString());
            CheckTrayAutoStart();
            var settings = new FakeSettings();
            var secrets = new SecretStorageService();
            settings.Current.Translation.ApiKeyEncrypted = secrets.Protect("ui-test-placeholder");
            var window = new SettingsWindow(settings, secrets, new FakeModels(), new HotkeyManager());
            settings.FailSave = true;
            Invoke(window, "OnClearApiKey");
            Check(settings.Current.Translation.ApiKeyEncrypted.Length > 0, "清除失败必须保留已保存密钥");
            Check(((PasswordBox)window.FindName("ApiKeyBox")).Password.Length > 0, "清除失败必须保留输入值");
            settings.FailSave = false;
            Invoke(window, "OnClearApiKey");
            Check(settings.Current.Translation.ApiKeyEncrypted == "" && settings.SavedKey == "", "清除成功必须同时更新持久化状态");
            Check(((PasswordBox)window.FindName("ApiKeyBox")).Password == "" && ((TextBox)window.FindName("ApiKeyVisibleBox")).Text == "", "明文密码框均应清空");
            var bitmap = new RenderTargetBitmap(640, 180, 96, 96, PixelFormats.Pbgra32);
            var drawing = new DrawingVisual();
            using (var dc = drawing.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 640, 180));
                dc.DrawText(new FormattedText("ScreenshotTool 自测示例\n拖动贴图，检查工具栏跟随。", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Microsoft YaHei"), 22, Brushes.Black, 1), new Point(24, 36));
            }
            bitmap.Render(drawing); bitmap.Freeze();
            var oneLineTextPin = ClipboardTextRenderer.Render("一行文字", out var oneLineTruncated);
            var multiLineTextPin = ClipboardTextRenderer.Render("第一行\n\n第三行，保留空行。", out var multiLineTruncated);
            Check(!oneLineTruncated && !multiLineTruncated, "普通剪贴板文本不应被截断");
            Check(multiLineTextPin.PixelHeight > oneLineTextPin.PixelHeight, "文本贴图必须保留换行和空白段落");
            Check(ClipboardTextRenderer.Render(new string('字', 15001), out var longTextTruncated).PixelHeight > 0 && longTextTruncated,
                "超长剪贴板文本必须安全截断并继续生成贴图");
            CheckSelectionGeometry(bitmap);
            var pin = new PinWindow(bitmap, new AnnotationRenderer());
            pin.ShowTranslationResult(new OcrResult { Text = "Test" }, string.Join("\n", Enumerable.Repeat("长译文自测：内容必须完整显示，不能被原图高度裁切。", 100)));
            Check(pin.RenderedImage.PixelHeight > bitmap.PixelHeight * 5, "长译文应按文本高度增长");
            Check(ReferenceEquals(pin.RecognitionImage, bitmap), "提取源应保持原图");
            Check(ReferenceEquals(pin.RenderedImage, pin.RenderedImage), "译文渲染应使用缓存");
            Check(window.ResizeMode == ResizeMode.NoResize, "设置窗体固定尺寸");
            var ocr = new OcrResultWindow("第一行文字\n第二行文字\n\n第二段，支持编辑。 ");
            Check(ocr.Topmost && ocr.ResizeMode == ResizeMode.NoResize, "提取窗体置顶且固定尺寸");
            if (!args.Contains("--interactive"))
            {
                window.Show(); window.UpdateLayout();
                CheckButtonsVisible(window);
                window.Hide();
                var loadingPin = new PinWindow(bitmap, new AnnotationRenderer());
                loadingPin.Show();
                loadingPin.ShowTranslationLoading();
                loadingPin.UpdateLayout();
                var cancelButton = (Button)loadingPin.FindName("CancelTranslationButton");
                var cancelPosition = cancelButton.TranslatePoint(new Point(), loadingPin);
                Check(cancelButton.IsVisible && cancelButton.ActualHeight >= 31 &&
                      cancelPosition.Y >= 0 && cancelPosition.Y + cancelButton.ActualHeight <= loadingPin.ActualHeight + 1,
                    "翻译加载取消按钮必须完整显示在贴图窗口内");
                loadingPin.Close();
                pin.Show(); pin.UpdateLayout();
                ((Button)pin.FindName("PinnedAnnotationButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                pin.UpdateLayout();
                application.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Check(!((Button)pin.FindName("UndoButton")).IsEnabled && !((Button)pin.FindName("RedoButton")).IsEnabled, "空历史撤销重做必须禁用");
                Check(((System.Windows.Controls.Primitives.Popup)pin.FindName("AnnotationToolbar")).IsOpen, "开启标注应显示工具栏");
                CheckPopupOwner(pin, "PinActionToolbar");
                CheckPopupOwner(pin, "AnnotationToolbar");
                pin.Hide();
                Check(!((System.Windows.Controls.Primitives.Popup)pin.FindName("PinActionToolbar")).IsOpen && !((System.Windows.Controls.Primitives.Popup)pin.FindName("AnnotationToolbar")).IsOpen, "隐藏贴图必须同时隐藏工具栏");
                var closePin = new PinWindow(bitmap, new AnnotationRenderer());
                var pinClosed = false;
                closePin.Closed += (_, _) => pinClosed = true;
                closePin.Show();
                var doubleClick = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonDownEvent
                };
                typeof(System.Windows.Input.MouseButtonEventArgs).GetProperty("ClickCount")!.SetValue(doubleClick, 2);
                typeof(PinWindow).GetMethod("OnMouseLeftButtonDown", BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null, types: [typeof(object), typeof(System.Windows.Input.MouseButtonEventArgs)], modifiers: null)!
                    .Invoke(closePin, [closePin, doubleClick]);
                Check(pinClosed, "贴图区域左键双击应关闭贴图");
                Console.WriteLine("Window layout and toolbar lifecycle checks passed.");
            }
            Console.WriteLine("UI contract checks passed: key failure/success, long translation, source image, render cache, fixed windows.");
            if (args.Contains("--interactive"))
            {
                application.ShutdownMode = ShutdownMode.OnLastWindowClose;
                pin.ShowTranslationResult(new OcrResult { Text = "Test" }, "这是独立的翻译阅读卡片。\n\n测试使用本地示例，不会发送网络请求。\n\n可以展开、收起，复制译文或上下对照图。");
                if (args.Contains("--pin"))
                {
                    pin.ShowInTaskbar = true; pin.Left = 100; pin.Top = 150; pin.Show();
                    ((Button)pin.FindName("PinnedAnnotationButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                else if (args.Contains("--ocr")) ocr.Show();
                else window.Show();
                application.Run();
            }
            else { pin.Close(); window.Close(); ocr.Close(); application.Shutdown(); }
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
    private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, [target, new RoutedEventArgs()]);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void CheckTrayAutoStart()
    {
        var startup = new FakeStartupRegistration();
        using var tray = new TrayIconService(new FakeLogger(), startup);
        tray.Start();
        var icon = (System.Windows.Forms.NotifyIcon)typeof(TrayIconService)
            .GetField("_notifyIcon", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tray)!;
        var item = icon.ContextMenuStrip!.Items.OfType<System.Windows.Forms.ToolStripMenuItem>()
            .Single(menuItem => menuItem.Text == "开机自启动");
        Check(!item.Checked, "未启用时开机自启动菜单不应勾选");
        item.PerformClick();
        Check(startup.Enabled, "点击开机自启动应启用启动项");
        item.PerformClick();
        Check(!startup.Enabled, "再次点击开机自启动应删除启动项");
        Console.WriteLine("Tray auto-start toggle checks passed.");
    }
    private static void CheckSelectionGeometry(BitmapSource bitmap)
    {
        var bounds = new VirtualScreenBounds(-1920, 0, 1920, 1080);
        var overlay = new CaptureOverlayWindow(bitmap, bounds);
        Check(((Button)overlay.FindName("CaptureAnnotationButton")).Content?.ToString() == "标", "F1 选区工具栏必须提供标注入口");
        Check(Enum.IsDefined(CaptureAction.Annotate), "截图动作必须支持直接标注");
        var type = typeof(CaptureOverlayWindow);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var original = new CaptureRegion(-1800, 100, 400, 250);
        type.GetField("_dragStartRegion", flags)!.SetValue(overlay, original);
        type.GetField("_dragStartPoint", flags)!.SetValue(overlay, (-1700, 150));
        var moved = (CaptureRegion)type.GetMethod("MoveSelection", flags)!.Invoke(overlay, [(-1650, 180)])!;
        Check(moved == new CaptureRegion(-1750, 130, 400, 250), "移动应保留宽高并应用偏移");
        var edgeType = type.GetNestedType("ResizeEdges", BindingFlags.NonPublic)!;
        foreach (var edges in new[] { 1, 2, 4, 8, 3, 6, 9, 12 })
        {
            type.GetField("_resizeEdges", flags)!.SetValue(overlay, Enum.ToObject(edgeType, edges));
            foreach (var point in new[] { (-3000, -500), (3000, 2000), (-1600, 200) })
            {
                var resized = (CaptureRegion)type.GetMethod("ResizeSelection", flags)!.Invoke(overlay, [point])!;
                Check(!resized.IsEmpty && resized == resized.ClampTo(bounds), "八个缩放方向不能越界或产生空选区");
            }
        }
        var cancelled = false;
        overlay.CaptureCancelled += (_, _) => cancelled = true;
        var click = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left);
        click.RoutedEvent = UIElement.MouseLeftButtonDownEvent;
        typeof(System.Windows.Input.MouseButtonEventArgs).GetProperty("ClickCount")!.SetValue(click, 2);
        type.GetMethod("OnSelectionMouseLeftButtonDown", flags)!.Invoke(overlay, [overlay, click]);
        Check(cancelled && click.Handled, "已框选区域双击应退出截图");
        overlay.Close();
        Console.WriteLine("Selection checks passed: negative-screen movement, eight resize directions, selected double-click exits capture.");
    }
    private static void CheckPopupOwner(PinWindow window, string popupName)
    {
        var popup = (System.Windows.Controls.Primitives.Popup)window.FindName(popupName);
        var child = (Visual)popup.Child;
        var popupSource = (System.Windows.Interop.HwndSource?)PresentationSource.FromVisual(child);
        var windowHandle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        Check(popupSource is not null, $"{popupName} 必须具有原生窗口");
        Check(ScreenshotTool.Core.Win32.NativeMethods.GetWindowLongPtr(
                popupSource!.Handle,
                ScreenshotTool.Core.Win32.NativeMethods.GwlHwndParent) == windowHandle,
            $"{popupName} 必须归属于贴图窗口，避免跨虚拟桌面残留");
    }
    private static void CheckButtonsVisible(Window window)
    {
        foreach (var button in Descendants(window).OfType<Button>().Where(button => button.IsVisible && button.Content is string))
        {
            var point = button.TranslatePoint(new Point(), window);
            Check(point.Y >= 0 && point.Y + button.ActualHeight <= window.ActualHeight + 1, $"按钮越界：{button.Content}");
        }
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private sealed class FakeSettings : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public bool FailSave { get; set; }
        public string SavedKey { get; private set; } = "";
        public void Load() { }
        public void Save() { if (FailSave) throw new IOException("Simulated write failure"); SavedKey = Current.Translation.ApiKeyEncrypted; }
    }
    private sealed class FakeModels : ITranslationModelService
    {
        public Task<IReadOnlyList<string>> GetAvailableModelsAsync(string url, string key, CancellationToken token = default) => Task.FromResult<IReadOnlyList<string>>(["test-model"]);
    }
    private sealed class FakeStartupRegistration : IStartupRegistrationService
    {
        public bool Enabled { get; private set; }
        public bool IsEnabled() => Enabled;
        public void SetEnabled(bool enabled) => Enabled = enabled;
    }
    private sealed class FakeLogger : IAppLogger
    {
        public void Information(string message) { }
        public void Error(string message, Exception exception) => throw new InvalidOperationException(message, exception);
    }
}
