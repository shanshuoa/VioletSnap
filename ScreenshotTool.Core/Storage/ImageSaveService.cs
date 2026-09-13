using System.Windows.Media.Imaging;

using System.IO;

namespace ScreenshotTool.Core.Storage;

public sealed class ImageSaveService : IImageSaveService
{
    public Task SaveAsync(BitmapSource bitmap, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return Task.Run(() => Save(bitmap, filePath, cancellationToken), cancellationToken);
    }

    private static void Save(BitmapSource bitmap, string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        BitmapEncoder encoder = extension switch
        {
            ".png" => new PngBitmapEncoder(),
            ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
            _ => throw new NotSupportedException("仅支持 PNG 和 JPG 格式。")
        };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }
}
