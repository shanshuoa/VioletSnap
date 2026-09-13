using System.Diagnostics;
using System.Runtime.InteropServices;
using ScreenshotTool.Core.Win32;

namespace ScreenshotTool.Core.Capture;

public sealed class WindowDetectionService
{
    public CaptureRegion? FindWindowAt(int x, int y)
    {
        CaptureRegion? found = null;
        var currentProcess = (uint)Environment.ProcessId;
        NativeMethods.EnumWindows((window, _) =>
        {
            if (!NativeMethods.IsWindowVisible(window)) return true;
            NativeMethods.GetWindowThreadProcessId(window, out var processId);
            if (processId == currentProcess) return true;
            if ((NativeMethods.GetWindowLongPtr(window, NativeMethods.GwlExStyle).ToInt64() & NativeMethods.WsExToolWindow) != 0) return true;
            var rectangle = GetBounds(window);
            if (rectangle.Right <= rectangle.Left || rectangle.Bottom <= rectangle.Top) return true;
            if (x < rectangle.Left || x >= rectangle.Right || y < rectangle.Top || y >= rectangle.Bottom) return true;
            found = new CaptureRegion(rectangle.Left, rectangle.Top, rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);
            return false;
        }, IntPtr.Zero);
        return found;
    }

    private static NativeRectangle GetBounds(IntPtr window)
    {
        if (NativeMethods.DwmGetWindowAttribute(window, NativeMethods.DwmwaExtendedFrameBounds, out var bounds, Marshal.SizeOf<NativeRectangle>()) == 0)
            return bounds;
        NativeMethods.GetWindowRect(window, out bounds);
        return bounds;
    }
}
