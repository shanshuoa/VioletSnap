using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace ScreenshotTool.App.Views;

internal static class FixedWindowLayout
{
    public static void Fit(Window window, double designWidth, double designHeight)
    {
        window.SourceInitialized += (_, _) =>
        {
            var area = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(window).Handle).WorkingArea;
            var dpi = VisualTreeHelper.GetDpi(window);
            var scale = Math.Min(1, Math.Min((area.Width / dpi.DpiScaleX - 24) / designWidth,
                (area.Height / dpi.DpiScaleY - 24) / designHeight));
            if (scale >= 1 || window.Content is not FrameworkElement content) return;
            window.Content = null;
            content.Width = designWidth; content.Height = designHeight;
            window.Content = new Viewbox { Stretch = Stretch.Uniform, Child = content };
            window.Width = designWidth * scale; window.Height = designHeight * scale;
        };
    }
}
