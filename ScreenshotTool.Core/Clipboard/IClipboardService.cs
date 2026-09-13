using System.Windows.Media.Imaging;

namespace ScreenshotTool.Core.Clipboard;

public interface IClipboardService
{
    Task SetImageAsync(BitmapSource image, CancellationToken cancellationToken = default);
    bool ContainsImage();
    BitmapSource? GetImage();
}
