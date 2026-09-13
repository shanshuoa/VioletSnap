using System.Windows;
using ScreenshotTool.Core.Ocr;

namespace ScreenshotTool.Tests;

internal static class OcrContractTests
{
    public static void ExtractedTextShouldPreserveDetectedLinesAndParagraphs()
    {
        var lines = new[]
        {
            new OcrLineInfo { Text = "第一行", BoundingRect = new Rect(10, 0, 80, 20) },
            new OcrLineInfo { Text = "第二行", BoundingRect = new Rect(10, 24, 80, 20) },
            new OcrLineInfo { Text = "下一段", BoundingRect = new Rect(10, 62, 80, 20) }
        };

        var text = OcrTextFormatter.PreserveLineLayout(lines);
        if (text != "第一行" + Environment.NewLine + "第二行" + Environment.NewLine + Environment.NewLine + "下一段")
            throw new InvalidOperationException($"OCR 分行格式未正确保留：{text}");
    }
}
