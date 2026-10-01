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
    internal static string LastPinFailure { get; private set; } = string.Empty;
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int SwShowNoActivate = 4;
    private static readonly Guid VirtualDesktopManagerClassId = new("AA509086-5CA9-4C25-8F95-589D3C07B48A");
    private static readonly Guid ImmersiveShellClassId = new("C2F03A33-21F5-47FA-B4BB-156362A2F239");
    private static readonly Guid ApplicationViewCollectionId = new("1841C6D7-4F9D-42C0-AF41-8747538F10E5");
    private static readonly Guid VirtualDesktopPinnedAppsClassId = new("B5A399E7-1C87-46B8-88E9-FC5747B171BD");
    private static readonly Guid VirtualDesktopPinnedAppsId = new("4CE81583-1E4C-4632-A621-07A53543148F");
    private static bool _available = true;

    /// <summary>
    /// Pins a single picture window to every Windows virtual desktop. This is
    /// separate from WS_EX_TOPMOST, whose scope is only the current desktop.
    /// </summary>
    public static bool TrySetPinnedAcrossDesktops(IntPtr windowHandle, bool pinned)
    {
        LastPinFailure = string.Empty;
        if (windowHandle == IntPtr.Zero)
            return FailPin("window handle is zero");

        object? shell = null;
        object? viewsObject = null;
        object? pinnedAppsObject = null;
        IApplicationView? view = null;
        IntPtr servicePointer = IntPtr.Zero;
        try
        {
            var shellType = Type.GetTypeFromCLSID(ImmersiveShellClassId, throwOnError: true)
                ?? throw new COMException("无法创建 Windows 虚拟桌面 Shell 服务。");
            shell = Activator.CreateInstance(shellType)!;
            var serviceProvider = (IServiceProvider)shell;

            var serviceId = ApplicationViewCollectionId;
            var interfaceId = ApplicationViewCollectionId;
            var result = serviceProvider.QueryService(ref serviceId, ref interfaceId, out servicePointer);
            if (result < 0 ||
                servicePointer == IntPtr.Zero)
            {
                return FailPin($"ApplicationViewCollection QueryService failed: 0x{result:X8}");
            }
            viewsObject = Marshal.GetObjectForIUnknown(servicePointer);
            Marshal.Release(servicePointer);
            servicePointer = IntPtr.Zero;
            if (viewsObject is not IApplicationViewCollection views ||
                views.GetViewForHwnd(windowHandle, out view) < 0 || view is null)
            {
                return FailPin("GetViewForHwnd failed");
            }

            serviceId = VirtualDesktopPinnedAppsClassId;
            interfaceId = VirtualDesktopPinnedAppsId;
            result = serviceProvider.QueryService(ref serviceId, ref interfaceId, out servicePointer);
            if (result < 0 ||
                servicePointer == IntPtr.Zero)
            {
                return FailPin($"VirtualDesktopPinnedApps QueryService failed: 0x{result:X8}");
            }
            pinnedAppsObject = Marshal.GetObjectForIUnknown(servicePointer);
            Marshal.Release(servicePointer);
            servicePointer = IntPtr.Zero;
            if (pinnedAppsObject is not IVirtualDesktopPinnedApps pinnedApps)
            {
                return FailPin("VirtualDesktopPinnedApps interface unavailable");
            }

            var isPinned = pinnedApps.IsViewPinned(view);
            if (pinned && !isPinned)
                pinnedApps.PinView(view);
            else if (!pinned && isPinned)
                pinnedApps.UnpinView(view);
            return pinnedApps.IsViewPinned(view) == pinned || FailPin("pin state was not applied");
        }
        catch (COMException exception)
        {
            return FailPin($"COM 0x{exception.HResult:X8}: {exception.Message}");
        }
        catch (PlatformNotSupportedException)
        {
            return FailPin("platform not supported");
        }
        catch (Exception exception)
        {
            return FailPin($"{exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            if (servicePointer != IntPtr.Zero)
                Marshal.Release(servicePointer);
            ReleaseComObject(view);
            ReleaseComObject(pinnedAppsObject);
            ReleaseComObject(viewsObject);
            ReleaseComObject(shell);
        }
    }

    private static bool FailPin(string reason)
    {
        LastPinFailure = reason;
        return false;
    }

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
            var result = manager.GetWindowDesktopId(windowHandle, out var windowDesktopId);
            if (result < 0)
                return false;

            var currentDesktopId = GetCurrentDesktopId(manager, windowHandle);
            if (currentDesktopId == Guid.Empty || currentDesktopId == windowDesktopId)
                return false;

            result = manager.MoveWindowToDesktop(windowHandle, ref currentDesktopId);
            if (result < 0)
                return false;

            NativeMethods.ShowWindowAsync(windowHandle, SwShowNoActivate);
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
            ReleaseComObject(manager);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        try
        {
            if (value is not null && Marshal.IsComObject(value))
                Marshal.FinalReleaseComObject(value);
        }
        catch
        {
            // COM cleanup must never terminate the resident screenshot app.
        }
    }

    private static Guid GetCurrentDesktopId(IVirtualDesktopManager manager, IntPtr pinWindowHandle)
    {
        var foregroundWindow = NativeMethods.GetForegroundWindow();
        if (foregroundWindow != IntPtr.Zero && foregroundWindow != pinWindowHandle &&
            manager.GetWindowDesktopId(foregroundWindow, out var foregroundDesktopId) >= 0 &&
            foregroundDesktopId != Guid.Empty)
        {
            return foregroundDesktopId;
        }

        using var probe = CreateCurrentDesktopProbe();
        return manager.GetWindowDesktopId(probe.Handle, out var probeDesktopId) >= 0
            ? probeDesktopId
            : Guid.Empty;
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

    [ComImport]
    [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IServiceProvider
    {
        [PreserveSig]
        int QueryService(
            [In] ref Guid serviceId,
            [In] ref Guid interfaceId,
            out IntPtr service);
    }

    [ComImport]
    [Guid("1841C6D7-4F9D-42C0-AF41-8747538F10E5")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationViewCollection
    {
        [PreserveSig] int GetViews([MarshalAs(UnmanagedType.Interface)] out object views);
        [PreserveSig] int GetViewsByZOrder([MarshalAs(UnmanagedType.Interface)] out object views);
        [PreserveSig] int GetViewsByAppUserModelId(IntPtr appUserModelId, [MarshalAs(UnmanagedType.Interface)] out object views);
        [PreserveSig] int GetViewForHwnd(IntPtr windowHandle, out IApplicationView view);
    }

    [ComImport]
    [Guid("372E1D3B-38D3-42E4-A15B-8AB2B178F513")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationView
    {
    }

    [ComImport]
    [Guid("4CE81583-1E4C-4632-A621-07A53543148F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IVirtualDesktopPinnedApps
    {
        [return: MarshalAs(UnmanagedType.Bool)]
        bool IsAppIdPinned(IntPtr appId);
        void PinAppID(IntPtr appId);
        void UnpinAppID(IntPtr appId);
        [return: MarshalAs(UnmanagedType.Bool)]
        bool IsViewPinned(IApplicationView view);
        void PinView(IApplicationView view);
        void UnpinView(IApplicationView view);
    }
}
