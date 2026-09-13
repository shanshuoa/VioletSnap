using System.Windows.Media.Imaging;

namespace ScreenshotTool.Core.Ocr;

public interface IOcrService
{
    Task<OcrResult> RecognizeAsync(BitmapSource bitmap, CancellationToken cancellationToken = default);
}
