using System.Text;

namespace ScreenshotTool.Core.Ocr;

public static class OcrTextFormatter
{
    public static string PreserveLineLayout(IReadOnlyList<OcrLineInfo> lines)
    {
        var visibleLines = lines.Where(line => !string.IsNullOrWhiteSpace(line.Text)).ToArray();
        if (visibleLines.Length == 0) return string.Empty;

        var text = new StringBuilder();
        for (var index = 0; index < visibleLines.Length; index++)
        {
            var current = visibleLines[index];
            if (index > 0)
            {
                var previous = visibleLines[index - 1];
                text.AppendLine();

                // OCR 行间的明显垂直留白按段落空行保留，普通行距仅保留一次换行。
                var lineHeight = Math.Max(1, Math.Max(previous.BoundingRect.Height, current.BoundingRect.Height));
                var verticalGap = current.BoundingRect.Top - previous.BoundingRect.Bottom;
                if (verticalGap >= lineHeight * 0.7)
                    text.AppendLine();
            }

            text.Append(current.Text.TrimEnd());
        }

        return text.ToString();
    }
}
