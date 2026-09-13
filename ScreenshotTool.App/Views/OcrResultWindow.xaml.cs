using System.Windows;
using System.Windows.Input;

namespace ScreenshotTool.App.Views;

public partial class OcrResultWindow : Window
{
    public OcrResultWindow(string text)
    {
        InitializeComponent();
        FixedWindowLayout.Fit(this, 720, 540);
        SourceText.Text = text;
    }

    private async void OnCopySource(object sender, RoutedEventArgs e)
    {
        try
        {
            await ScreenshotTool.Core.Clipboard.TextClipboard.CopyAsync(SourceText.Text);
            CopyStatusText.Text = "已复制到剪贴板";
        }
        catch { CopyStatusText.Text = "剪贴板暂时被占用，请重试"; }
    }

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1 && e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private async void OnCopyParagraphs(object sender, RoutedEventArgs e)
    {
        var paragraphs = System.Text.RegularExpressions.Regex.Split(SourceText.Text.Replace("\r\n", "\n"), @"\n\s*\n");
        var text = string.Join(Environment.NewLine + Environment.NewLine, paragraphs.Select(paragraph =>
            System.Text.RegularExpressions.Regex.Replace(paragraph, @"\s*\n\s*", " ")));
        try { await ScreenshotTool.Core.Clipboard.TextClipboard.CopyAsync(text); CopyStatusText.Text = "已合并复制"; }
        catch { CopyStatusText.Text = "复制失败，请重试"; }
    }
    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
