using System.Windows.Input;
using ScreenshotTool.Core.Win32;

namespace ScreenshotTool.Core.Hotkeys;

public readonly record struct HotkeyGesture(uint Modifiers, uint VirtualKey, string DisplayText)
{
    public static bool TryParse(string? text, out HotkeyGesture gesture, out string error)
    {
        gesture = default;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return true;

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            error = "快捷键格式无效。";
            return false;
        }

        var modifiers = ModifierKeys.None;
        for (var index = 0; index < parts.Length - 1; index++)
        {
            switch (parts[index].ToUpperInvariant())
            {
                case "CTRL": case "CONTROL": modifiers |= ModifierKeys.Control; break;
                case "ALT": modifiers |= ModifierKeys.Alt; break;
                case "SHIFT": modifiers |= ModifierKeys.Shift; break;
                case "WIN": case "WINDOWS": modifiers |= ModifierKeys.Windows; break;
                default:
                    error = $"无法识别修饰键“{parts[index]}”。";
                    return false;
            }
        }

        if (!Enum.TryParse<Key>(parts[^1], true, out var key) || !TryCreate(key, modifiers, out gesture, out error))
        {
            if (string.IsNullOrEmpty(error)) error = $"无法识别按键“{parts[^1]}”。";
            return false;
        }
        return true;
    }

    public static bool TryCreate(Key key, ModifierKeys modifiers, out HotkeyGesture gesture, out string error)
    {
        gesture = default;
        error = string.Empty;
        if (IsModifierKey(key) || key is Key.None or Key.System or Key.DeadCharProcessed)
        {
            error = "请按下一个非修饰键。";
            return false;
        }

        if (modifiers == ModifierKeys.None && key is >= Key.A and <= Key.Z or >= Key.D0 and <= Key.D9)
        {
            error = "字母或数字快捷键至少需要搭配 Ctrl、Alt、Shift 或 Win。";
            return false;
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey <= 0)
        {
            error = "该按键不能注册为全局快捷键。";
            return false;
        }

        var nativeModifiers = NativeMethods.ModNoRepeat;
        var labels = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) { nativeModifiers |= NativeMethods.ModControl; labels.Add("Ctrl"); }
        if (modifiers.HasFlag(ModifierKeys.Alt)) { nativeModifiers |= NativeMethods.ModAlt; labels.Add("Alt"); }
        if (modifiers.HasFlag(ModifierKeys.Shift)) { nativeModifiers |= NativeMethods.ModShift; labels.Add("Shift"); }
        if (modifiers.HasFlag(ModifierKeys.Windows)) { nativeModifiers |= NativeMethods.ModWin; labels.Add("Win"); }
        labels.Add(key.ToString());
        gesture = new HotkeyGesture(nativeModifiers, (uint)virtualKey, string.Join('+', labels));
        return true;
    }

    public static bool IsModifierKey(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;
}

public sealed record HotkeyRegistrationResult(bool ScreenshotRegistered, bool PinRegistered, IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0;
}
