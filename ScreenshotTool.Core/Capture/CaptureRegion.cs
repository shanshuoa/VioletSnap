namespace ScreenshotTool.Core.Capture;

public readonly record struct CaptureRegion(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static CaptureRegion FromPoints(int startX, int startY, int endX, int endY)
    {
        var left = Math.Min(startX, endX);
        var top = Math.Min(startY, endY);
        return new CaptureRegion(left, top, Math.Abs(endX - startX), Math.Abs(endY - startY));
    }

    public CaptureRegion ClampTo(VirtualScreenBounds bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return default;
        var boundsRight = (long)bounds.X + bounds.Width;
        var boundsBottom = (long)bounds.Y + bounds.Height;
        var left = Math.Clamp((long)X, bounds.X, boundsRight - 1);
        var top = Math.Clamp((long)Y, bounds.Y, boundsBottom - 1);
        var right = Math.Clamp((long)X + Math.Max(1, Width), left + 1, boundsRight);
        var bottom = Math.Clamp((long)Y + Math.Max(1, Height), top + 1, boundsBottom);
        return new CaptureRegion((int)left, (int)top, (int)(right - left), (int)(bottom - top));
    }
}
