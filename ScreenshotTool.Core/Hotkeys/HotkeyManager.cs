using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using ScreenshotTool.Core.Win32;

namespace ScreenshotTool.Core.Hotkeys;

public sealed class HotkeyManager : IHotkeyManager
{
    private const int ScreenshotHotkeyId = 1;
    private const int PinHotkeyId = 2;
    private HwndSource? _source;
    private bool _screenshotRegistered;
    private bool _pinRegistered;

    public event EventHandler? ScreenshotRequested;
    public event EventHandler? PinRequested;

    public HotkeyRegistrationResult Register(string screenshotHotkey, string pinHotkey)
    {
        EnsureSource();
        UnregisterCurrent();

        var errors = new List<string>();
        _screenshotRegistered = TryRegister(ScreenshotHotkeyId, "截图", screenshotHotkey, errors);
        _pinRegistered = TryRegister(PinHotkeyId, "贴图", pinHotkey, errors);
        return new HotkeyRegistrationResult(_screenshotRegistered, _pinRegistered, errors);
    }

    private void EnsureSource()
    {
        if (_source is not null) return;
        _source = new HwndSource(new HwndSourceParameters("ScreenshotToolHotkeys")
        {
            ParentWindow = NativeMethods.HwndMessage,
            Width = 0,
            Height = 0,
            WindowStyle = 0
        });
        _source.AddHook(WndProc);
    }

    private bool TryRegister(int id, string actionName, string hotkeyText, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(hotkeyText)) return false;
        if (!HotkeyGesture.TryParse(hotkeyText, out var gesture, out var parseError))
        {
            errors.Add($"{actionName}快捷键无效：{parseError}");
            return false;
        }

        if (NativeMethods.RegisterHotKey(_source!.Handle, id, gesture.Modifiers, gesture.VirtualKey)) return true;
        var errorCode = Marshal.GetLastWin32Error();
        var reason = new Win32Exception(errorCode).Message;
        errors.Add($"{actionName}快捷键 {gesture.DisplayText} 注册失败，可能已被其他软件占用（{reason}）。");
        return false;
    }

    private void UnregisterCurrent()
    {
        if (_source is null) return;
        if (_screenshotRegistered) NativeMethods.UnregisterHotKey(_source.Handle, ScreenshotHotkeyId);
        if (_pinRegistered) NativeMethods.UnregisterHotKey(_source.Handle, PinHotkeyId);
        _screenshotRegistered = false;
        _pinRegistered = false;
    }

    public void Dispose()
    {
        if (_source is null) return;
        UnregisterCurrent();
        _source.RemoveHook(WndProc);
        _source.Dispose();
        _source = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmHotkey && wParam.ToInt32() == ScreenshotHotkeyId)
        {
            ScreenshotRequested?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        else if (message == NativeMethods.WmHotkey && wParam.ToInt32() == PinHotkeyId)
        {
            PinRequested?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }
}
