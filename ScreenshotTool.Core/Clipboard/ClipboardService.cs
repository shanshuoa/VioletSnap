using System.Runtime.InteropServices;
using System.IO;
using System.Windows.Media;
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
                return DecodeImage(System.Windows.Clipboard.GetDataObject());
            }
            catch (COMException) when (attempt < 5)
            {
                Thread.Sleep(20 * attempt);
            }
        }

        return null;
    }

    private static BitmapSource? DecodeImage(IDataObject? data)
    {
        if (data is null) return null;
        string[] formats = ["PNG", "image/png", "JFIF", "image/jpeg", "image/gif", "image/bmp", "image/tiff", "image/webp"];
        foreach (var format in formats)
        {
            try
            {
                if (!data.GetDataPresent(format)) continue;
                var value = data.GetData(format);
                if (value is byte[] bytes)
                {
                    using var stream = new MemoryStream(bytes, false);
                    if (Decode(stream) is { } image) return image;
                }
                else if (value is Stream source)
                {
                    using var stream = new MemoryStream();
                    if (source.CanSeek) source.Position = 0;
                    source.CopyTo(stream);
                    stream.Position = 0;
                    if (Decode(stream) is { } image) return image;
                }
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or COMException) { }
        }
        try
        {
            if (data.GetDataPresent(DataFormats.Bitmap) && data.GetData(DataFormats.Bitmap) is BitmapSource bitmap)
                return FixLegacyAlpha(bitmap);
        }
        catch (Exception ex) when (ex is COMException or ArgumentException) { }
        try
        {
            if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] paths)
                foreach (var path in paths)
                {
                    try
                    {
                        if (!File.Exists(path)) continue;
                        using var stream = File.OpenRead(path);
                        if (Decode(stream) is { } image) return image;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
                }
        }
        catch (Exception ex) when (ex is COMException or ArgumentException) { }
        return null;
    }

    private static BitmapSource? Decode(Stream stream)
    {
        try
        {
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0) return null;
            var frame = decoder.Frames[0];
            frame.Freeze();
            return frame;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or FileFormatException) { return null; }
    }

    private static BitmapSource FixLegacyAlpha(BitmapSource bitmap)
    {
        if (bitmap.Format != PixelFormats.Bgra32 && bitmap.Format != PixelFormats.Pbgra32)
        {
            if (bitmap.CanFreeze) bitmap.Freeze();
            return bitmap;
        }
        var stride = checked(bitmap.PixelWidth * 4);
        var pixels = new byte[checked(stride * bitmap.PixelHeight)];
        bitmap.CopyPixels(pixels, stride, 0);
        for (var offset = 3; offset < pixels.Length; offset += 4)
            if (pixels[offset] != 0)
            {
                if (bitmap.CanFreeze) bitmap.Freeze();
                return bitmap;
            }
        var result = BitmapSource.Create(bitmap.PixelWidth, bitmap.PixelHeight, bitmap.DpiX, bitmap.DpiY, PixelFormats.Bgr32, null, pixels, stride);
        result.Freeze();
        return result;
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
        using var png = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        encoder.Save(png);
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                png.Position = 0;
                var data = new DataObject();
                data.SetData(DataFormats.Bitmap, image);
                data.SetData("PNG", png);
                System.Windows.Clipboard.SetDataObject(data, true);
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
