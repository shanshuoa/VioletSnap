using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotTool.Core.Annotation.Models;

namespace ScreenshotTool.Core.Annotation;

public sealed class AnnotationRenderer : IAnnotationRenderer
{
    public BitmapSource Render(BitmapSource original, IReadOnlyList<AnnotationItem> annotations)
    {
        ArgumentNullException.ThrowIfNull(original);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawImage(original, new Rect(0, 0, original.PixelWidth, original.PixelHeight));
            foreach (var annotation in annotations)
            {
                Draw(context, annotation, original);
            }
        }

        var result = new RenderTargetBitmap(original.PixelWidth, original.PixelHeight, original.DpiX, original.DpiY, PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        return result;
    }

    private static void Draw(DrawingContext context, AnnotationItem item, BitmapSource original)
    {
        context.PushOpacity(item.Opacity);
        var pen = new Pen(new SolidColorBrush(item.StrokeColor), item.StrokeThickness) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        switch (item)
        {
            case RectangleAnnotation rectangle:
                context.DrawRectangle(rectangle.FillColor is { } fill ? new SolidColorBrush(fill) : null, pen, new Rect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height));
                break;
            case RoundedRectangleAnnotation rounded:
                context.DrawRoundedRectangle(null, pen, new Rect(rounded.X, rounded.Y, rounded.Width, rounded.Height), rounded.Radius, rounded.Radius);
                break;
            case LineAnnotation line:
                context.DrawLine(pen, line.Start, line.End);
                break;
            case ArrowAnnotation arrow:
                DrawArrow(context, pen, arrow);
                break;
            case PenAnnotation stroke when stroke.Points.Count > 1:
                var geometry = new StreamGeometry();
                using (var geometryContext = geometry.Open())
                {
                    geometryContext.BeginFigure(stroke.Points[0], false, false);
                    geometryContext.PolyLineTo(stroke.Points.Skip(1).ToList(), true, true);
                }
                geometry.Freeze();
                context.DrawGeometry(null, pen, geometry);
                break;
            case TextAnnotation text when !string.IsNullOrWhiteSpace(text.Text):
                var formatted = new FormattedText(text.Text, System.Globalization.CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight, new Typeface(new FontFamily(text.FontFamilyName), FontStyles.Normal, text.FontWeight, FontStretches.Normal),
                    text.FontSize, new SolidColorBrush(text.TextColor), 1.0);
                if (text.BackgroundColor is { } background)
                    context.DrawRectangle(new SolidColorBrush(background), null, new Rect(text.X, text.Y, formatted.WidthIncludingTrailingWhitespace, formatted.Height));
                context.DrawText(formatted, new Point(text.X, text.Y));
                break;
            case MosaicAnnotation mosaic:
                DrawEffectRegion(context, original, CreateMosaicRegion, mosaic, mosaic.PixelSize);
                break;
            case BlurAnnotation blur:
                DrawEffectRegion(context, original, CreateBlurRegion, blur, Math.Max(1, (int)Math.Round(blur.Radius)));
                break;
        }
        context.Pop();
    }

    private static void DrawArrow(DrawingContext context, Pen pen, ArrowAnnotation arrow)
    {
        context.DrawLine(pen, arrow.Start, arrow.End);
        var vector = arrow.Start - arrow.End;
        if (vector.Length < 0.01) return;
        vector.Normalize();
        var angle = arrow.ArrowHeadAngle * Math.PI / 180;
        var left = Rotate(vector, angle) * arrow.ArrowHeadLength;
        var right = Rotate(vector, -angle) * arrow.ArrowHeadLength;
        context.DrawLine(pen, arrow.End, arrow.End + left);
        context.DrawLine(pen, arrow.End, arrow.End + right);
    }

    private static Vector Rotate(Vector vector, double radians) => new(
        vector.X * Math.Cos(radians) - vector.Y * Math.Sin(radians),
        vector.X * Math.Sin(radians) + vector.Y * Math.Cos(radians));

    private static void DrawEffectRegion(DrawingContext context, BitmapSource source, Func<BitmapSource, Int32Rect, int, BitmapSource> createEffect, AnnotationItem item, int amount)
    {
        if (item.Width < 1 || item.Height < 1) return;
        var x = Math.Clamp((int)Math.Floor(item.X), 0, source.PixelWidth - 1);
        var y = Math.Clamp((int)Math.Floor(item.Y), 0, source.PixelHeight - 1);
        var width = Math.Clamp((int)Math.Ceiling(item.Width), 1, source.PixelWidth - x);
        var height = Math.Clamp((int)Math.Ceiling(item.Height), 1, source.PixelHeight - y);
        var region = new Int32Rect(x, y, width, height);
        context.DrawImage(createEffect(source, region, amount), new Rect(x, y, width, height));
    }

    private static BitmapSource CreateMosaicRegion(BitmapSource source, Int32Rect region, int pixelSize)
    {
        var pixels = CopyBgraRegion(source, region);
        var stride = region.Width * 4;
        pixelSize = Math.Max(2, pixelSize);
        for (var blockY = 0; blockY < region.Height; blockY += pixelSize)
        for (var blockX = 0; blockX < region.Width; blockX += pixelSize)
        {
            var sample = blockY * stride + blockX * 4;
            var maxY = Math.Min(region.Height, blockY + pixelSize);
            var maxX = Math.Min(region.Width, blockX + pixelSize);
            for (var y = blockY; y < maxY; y++)
            for (var x = blockX; x < maxX; x++)
            {
                var index = y * stride + x * 4;
                pixels[index] = pixels[sample]; pixels[index + 1] = pixels[sample + 1];
                pixels[index + 2] = pixels[sample + 2]; pixels[index + 3] = pixels[sample + 3];
            }
        }
        return CreateBitmap(pixels, region.Width, region.Height);
    }

    private static BitmapSource CreateBlurRegion(BitmapSource source, Int32Rect region, int radius)
    {
        var input = CopyBgraRegion(source, region);
        var temp = new byte[input.Length];
        var output = new byte[input.Length];
        BlurPass(input, temp, region.Width, region.Height, radius, horizontal: true);
        BlurPass(temp, output, region.Width, region.Height, radius, horizontal: false);
        return CreateBitmap(output, region.Width, region.Height);
    }

    private static void BlurPass(byte[] input, byte[] output, int width, int height, int radius, bool horizontal)
    {
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        for (var channel = 0; channel < 4; channel++)
        {
            var sum = 0; var count = 0;
            for (var offset = -radius; offset <= radius; offset++)
            {
                var sampleX = horizontal ? x + offset : x;
                var sampleY = horizontal ? y : y + offset;
                if (sampleX < 0 || sampleX >= width || sampleY < 0 || sampleY >= height) continue;
                sum += input[(sampleY * width + sampleX) * 4 + channel]; count++;
            }
            output[(y * width + x) * 4 + channel] = (byte)(sum / count);
        }
    }

    private static byte[] CopyBgraRegion(BitmapSource source, Int32Rect region)
    {
        var converted = source.Format == PixelFormats.Bgra32 ? source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[region.Width * region.Height * 4];
        converted.CopyPixels(region, pixels, region.Width * 4, 0);
        return pixels;
    }

    private static BitmapSource CreateBitmap(byte[] pixels, int width, int height)
    {
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }
}
