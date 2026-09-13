using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using ScreenshotTool.Core.Win32;

namespace ScreenshotTool.Core.Capture;

public sealed class GdiCaptureService : ICaptureService
{
    public VirtualScreenBounds GetVirtualScreenBounds() => new(
        NativeMethods.GetSystemMetrics(NativeMethods.SmXVirtualScreen),
        NativeMethods.GetSystemMetrics(NativeMethods.SmYVirtualScreen),
        NativeMethods.GetSystemMetrics(NativeMethods.SmCxVirtualScreen),
        NativeMethods.GetSystemMetrics(NativeMethods.SmCyVirtualScreen));

    public BitmapSource CaptureVirtualScreen()
    {
        var bounds = GetVirtualScreenBounds();
        return CaptureRegion(new CaptureRegion(bounds.X, bounds.Y, bounds.Width, bounds.Height));
    }

    public BitmapSource CaptureRegion(CaptureRegion region)
    {
        if (region.IsEmpty)
        {
            throw new ArgumentOutOfRangeException(nameof(region));
        }

        var screenDc = NativeMethods.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            NativeMethods.ThrowLastWin32Error("无法获取桌面 DC。");
        }

        IntPtr memoryDc = IntPtr.Zero;
        IntPtr bitmap = IntPtr.Zero;
        IntPtr previous = IntPtr.Zero;
        try
        {
            memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
            bitmap = NativeMethods.CreateCompatibleBitmap(screenDc, region.Width, region.Height);
            if (memoryDc == IntPtr.Zero || bitmap == IntPtr.Zero)
            {
                NativeMethods.ThrowLastWin32Error("无法创建截图缓冲区。");
            }

            previous = NativeMethods.SelectObject(memoryDc, bitmap);
            if (previous == IntPtr.Zero || !NativeMethods.BitBlt(memoryDc, 0, 0, region.Width, region.Height, screenDc, region.X, region.Y, NativeMethods.SrcCopy | NativeMethods.CaptureBlt))
            {
                NativeMethods.ThrowLastWin32Error("BitBlt 截图失败。");
            }

            var source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            if (previous != IntPtr.Zero && memoryDc != IntPtr.Zero)
            {
                NativeMethods.SelectObject(memoryDc, previous);
            }

            if (bitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(bitmap);
            }

            if (memoryDc != IntPtr.Zero)
            {
                NativeMethods.DeleteDC(memoryDc);
            }

            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }
}
