using System.Runtime.InteropServices;

namespace ScreenshotTool.Core.Win32;

[StructLayout(LayoutKind.Sequential)]
public struct WindowPosition
{
    public IntPtr WindowHandle;
    public IntPtr InsertAfter;
    public int X;
    public int Y;
    public int Width;
    public int Height;
    public uint Flags;
}

[StructLayout(LayoutKind.Sequential)]
public struct NativeRectangle
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential)]
public struct DataBlob
{
    public int Size;
    public IntPtr Data;
}
