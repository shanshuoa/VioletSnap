namespace ScreenshotTool.Core.Hotkeys;

public interface IHotkeyManager : IDisposable
{
    event EventHandler? ScreenshotRequested;
    event EventHandler? PinRequested;
    HotkeyRegistrationResult Register(string screenshotHotkey, string pinHotkey);
}
