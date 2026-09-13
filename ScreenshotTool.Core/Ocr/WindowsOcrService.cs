using System.Windows;
using System.Windows.Media.Imaging;
using Windows.Globalization;
using Windows.Media.Ocr;
using ScreenshotTool.Core.Configuration;
using ScreenshotTool.Core.Infrastructure;

namespace ScreenshotTool.Core.Ocr;

public sealed class WindowsOcrService(ISettingsService settings) : IOcrService
{
    public async Task<OcrResult> RecognizeAsync(BitmapSource bitmap, CancellationToken cancellationToken = default)
    {
        var scale = Math.Min(1, (double)OcrEngine.MaxImageDimension / Math.Max(bitmap.PixelWidth, bitmap.PixelHeight));
        if (scale < 1)
        {
            bitmap = new TransformedBitmap(bitmap, new System.Windows.Media.ScaleTransform(scale, scale));
            bitmap.Freeze();
        }
        using var softwareBitmap = await BitmapInterop.ToSoftwareBitmapAsync(bitmap, cancellationToken);
        var engines = CreateEngines(settings.Current.Ocr.PreferredLanguages).ToArray();
        if (engines.Length == 0) throw new InvalidOperationException("系统没有可用的文字识别语言包，请在 Windows 语言设置中安装：" + string.Join("、", settings.Current.Ocr.PreferredLanguages));

        OcrResult? bestResult = null;
        var bestScore = -1;
        foreach (var engine in engines)
        {
            var nativeResult = await engine.RecognizeAsync(softwareBitmap).AsTask(cancellationToken);
            var candidate = CreateResult(nativeResult, engine.RecognizerLanguage?.LanguageTag);
            var score = candidate.Text.Count(character => !char.IsWhiteSpace(character)) + candidate.Lines.Count * 2;
            if (score <= bestScore) continue;
            bestResult = candidate;
            bestScore = score;
        }
        if (bestResult is null) return new OcrResult();
        if (scale >= 1) return bestResult;
        return new OcrResult { Text = bestResult.Text, Language = bestResult.Language,
            Lines = bestResult.Lines.Select(line => new OcrLineInfo { Text = line.Text,
                BoundingRect = line.BoundingRect.IsEmpty ? Rect.Empty : new Rect(line.BoundingRect.X / scale,
                    line.BoundingRect.Y / scale, line.BoundingRect.Width / scale, line.BoundingRect.Height / scale) }).ToArray() };
    }

    private static OcrResult CreateResult(Windows.Media.Ocr.OcrResult result, string? languageTag)
    {
        var lines = result.Lines.Select(line =>
        {
            if (line.Words.Count == 0)
                return new OcrLineInfo { Text = line.Text, BoundingRect = Rect.Empty };

            var left = line.Words.Min(word => word.BoundingRect.X);
            var top = line.Words.Min(word => word.BoundingRect.Y);
            var right = line.Words.Max(word => word.BoundingRect.X + word.BoundingRect.Width);
            var bottom = line.Words.Max(word => word.BoundingRect.Y + word.BoundingRect.Height);
            return new OcrLineInfo
            {
                Text = line.Text,
                BoundingRect = new Rect(left, top, right - left, bottom - top)
            };
        }).ToList();
        return new OcrResult
        {
            Text = OcrTextFormatter.PreserveLineLayout(lines),
            Language = languageTag,
            Lines = lines
        };
    }

    private static IEnumerable<OcrEngine> CreateEngines(IEnumerable<string> preferred)
    {
        var createdTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in preferred)
        {
            var available = OcrEngine.AvailableRecognizerLanguages.FirstOrDefault(language =>
                string.Equals(language.LanguageTag, tag, StringComparison.OrdinalIgnoreCase) ||
                language.LanguageTag.StartsWith(tag + "-", StringComparison.OrdinalIgnoreCase) ||
                tag.StartsWith(language.LanguageTag + "-", StringComparison.OrdinalIgnoreCase));
            if (available is null || !createdTags.Add(available.LanguageTag)) continue;
            var engine = OcrEngine.TryCreateFromLanguage(available);
            if (engine is not null) yield return engine;
        }

        if (createdTags.Count == 0)
        {
            var fallback = OcrEngine.TryCreateFromUserProfileLanguages();
            if (fallback is not null) yield return fallback;
        }
    }
}
