using System.Windows;

namespace ScreenshotTool.Core.Ocr;

public sealed class OcrResult
{
    public string Text { get; init; } = string.Empty;
    public string? Language { get; init; }
    public IReadOnlyList<OcrLineInfo> Lines { get; init; } = [];
}

public sealed class OcrLineInfo
{
    public string Text { get; init; } = string.Empty;
    public Rect BoundingRect { get; init; }
}
