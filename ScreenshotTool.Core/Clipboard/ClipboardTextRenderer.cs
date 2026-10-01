using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenshotTool.Core.Clipboard;

public static class ClipboardTextRenderer
{
    private const int MaximumCharacters = 15000;
    private const int MaximumExplicitLines = 350;
    private const double MaximumTextWidth = 820;
    private const double MinimumTextWidth = 300;
    private const double HorizontalPadding = 34;
    private const double HeaderHeight = 54;
    private const double BottomPadding = 30;

    public static BitmapSource Render(string text, out bool truncated)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("剪贴板文本不能为空。", nameof(text));

        truncated = normalized.Length > MaximumCharacters;
        if (truncated)
            normalized = normalized[..MaximumCharacters];

        var lines = normalized.Split('\n');
        if (lines.Length > MaximumExplicitLines)
        {
            normalized = string.Join("\n", lines.Take(MaximumExplicitLines));
            truncated = true;
        }

        if (truncated)
            normalized += "\n\n……（内容过长，文本贴图已截断）";

        const double fontSize = 21;
        var bodyTypeface = new Typeface(
            new FontFamily("Microsoft YaHei UI"),
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);
        var body = new FormattedText(
            normalized,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            bodyTypeface,
            fontSize,
            new SolidColorBrush(Color.FromRgb(43, 36, 55)),
            1)
        {
            MaxTextWidth = MaximumTextWidth,
            LineHeight = 32,
            TextAlignment = TextAlignment.Left,
            Trimming = TextTrimming.None
        };

        var textWidth = Math.Clamp(
            Math.Ceiling(body.WidthIncludingTrailingWhitespace),
            MinimumTextWidth,
            MaximumTextWidth);
        body.MaxTextWidth = textWidth;

        var bitmapWidth = (int)Math.Ceiling(textWidth + HorizontalPadding * 2);
        var bitmapHeight = (int)Math.Ceiling(HeaderHeight + body.Height + BottomPadding);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            var background = new SolidColorBrush(Color.FromRgb(251, 249, 255));
            var header = new SolidColorBrush(Color.FromRgb(242, 237, 255));
            var accent = new SolidColorBrush(Color.FromRgb(118, 82, 213));
            var divider = new Pen(new SolidColorBrush(Color.FromRgb(222, 211, 250)), 1);
            drawing.DrawRectangle(background, null, new Rect(0, 0, bitmapWidth, bitmapHeight));
            drawing.DrawRectangle(header, null, new Rect(0, 0, bitmapWidth, HeaderHeight));
            drawing.DrawLine(divider, new Point(0, HeaderHeight), new Point(bitmapWidth, HeaderHeight));
            drawing.DrawEllipse(accent, null, new Point(HorizontalPadding + 6, HeaderHeight / 2), 5, 5);

            var title = new FormattedText(
                "剪贴板文本",
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei UI"),
                14,
                new SolidColorBrush(Color.FromRgb(91, 67, 142)),
                1);
            drawing.DrawText(title, new Point(HorizontalPadding + 20, 17));
            drawing.DrawText(body, new Point(HorizontalPadding, HeaderHeight + 18));
        }

        var bitmap = new RenderTargetBitmap(bitmapWidth, bitmapHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
