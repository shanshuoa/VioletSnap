using System.Runtime.InteropServices;
using System.Windows.Interop;
using ScreenshotTool.Core.Win32;

namespace ScreenshotTool.App.Services;

/// <summary>
/// Uses the documented Windows virtual-desktop manager to move a pinned image
/// to the currently active desktop. Failures are intentionally non-fatal so
/// unsupported Windows editions continue to use normal topmost behavior.
/// </summary>
internal static class VirtualDesktopFollowService
{
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private static readonly Guid VirtualDesktopManagerClassId = new("AA509086-5CA9-4C25-8F95-589D3C07B48A");
    private static bool _available = true;

    public static bool TryFollowCurrentDesktop(IntPtr windowHandle)
    {
        if (!_available || windowHandle == IntPtr.Zero)
            return false;

        IVirtualDesktopManager? manager = null;
        try
        {
            var managerType = Type.GetTypeFromCLSID(VirtualDesktopManagerClassId, throwOnError: true)
                ?? throw new COMException("无法创建 Windows 虚拟桌面管理器。");
            manager = (IVirtualDesktopManager)Activator.CreateInstance(managerType)!;
            var result = manager.IsWindowOnCurrentVirtualDesktop(windowHandle, out var isCurrent);
            if (result < 0)
                return false;
            if (isCurrent)
                return true;

            using var probe = CreateCurrentDesktopProbe();
            result = manager.GetWindowDesktopId(probe.Handle, out var currentDesktopId);
            if (result < 0 || currentDesktopId == Guid.Empty)
                return false;

            result = manager.MoveWindowToDesktop(windowHandle, ref currentDesktopId);
            if (result < 0)
                return false;

            NativeMethods.SetWindowPos(
                windowHandle,
                new IntPtr(-1),
                0,
                0,
                0,
                0,
                NativeMethods.SwpNoMove | NativeMethods.SwpNoSize |
                NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
            return true;
        }
        catch (COMException)
        {
            return false;
        }
        catch (PlatformNotSupportedException)
        {
            _available = false;
            return false;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            try
            {
                if (manager is not null && Marshal.IsComObject(manager))
                    Marshal.FinalReleaseComObject(manager);
            }
            catch
            {
                // COM cleanup must never terminate the resident screenshot app.
            }
        }
    }

    private static HwndSource CreateCurrentDesktopProbe()
    {
        var parameters = new HwndSourceParameters("VioletSnap.VirtualDesktopProbe")
        {
            Width = 1,
            Height = 1,
            PositionX = -32000,
            PositionY = -32000,
            WindowStyle = WsPopup,
            ExtendedWindowStyle = WsExToolWindow | WsExNoActivate
        };
        return new HwndSource(parameters);
    }

    [ComImport]
    [Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IVirtualDesktopManager
    {
        [PreserveSig]
        int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, [MarshalAs(UnmanagedType.Bool)] out bool onCurrentDesktop);

        [PreserveSig]
        int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);

        [PreserveSig]
        int MoveWindowToDesktop(IntPtr topLevelWindow, [In] ref Guid desktopId);
    }
}
