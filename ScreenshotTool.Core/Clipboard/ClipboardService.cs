using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ScreenshotTool.Core.Clipboard;

public sealed class ClipboardService : IClipboardService
{
    public bool ContainsImage() => System.Windows.Clipboard.ContainsImage();

    public bool ContainsText() => System.Windows.Clipboard.ContainsText(TextDataFormat.UnicodeText);

    public BitmapSource? GetImage()
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                var image = System.Windows.Clipboard.GetImage();
                if (image is not null && image.CanFreeze)
                {
                    image.Freeze();
                }

                return image;
            }
            catch (COMException) when (attempt < 5)
            {
                Thread.Sleep(20 * attempt);
            }
        }

        return null;
    }

    public string? GetText()
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                return System.Windows.Clipboard.ContainsText(TextDataFormat.UnicodeText)
                    ? System.Windows.Clipboard.GetText(TextDataFormat.UnicodeText)
                    : null;
            }
            catch (COMException) when (attempt < 5)
            {
                Thread.Sleep(20 * attempt);
            }
        }

        return null;
    }

    public async Task SetImageAsync(BitmapSource image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                System.Windows.Clipboard.SetImage(image);
                return;
            }
            catch (COMException) when (attempt < 5)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20 * attempt), cancellationToken);
            }
        }

        throw new COMException("无法写入剪贴板。");
    }
}
