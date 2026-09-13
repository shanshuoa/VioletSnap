using System.Windows.Media.Imaging;

namespace ScreenshotTool.Core.Storage;

public interface IImageSaveService
{
    Task SaveAsync(BitmapSource bitmap, string filePath, CancellationToken cancellationToken = default);
}
