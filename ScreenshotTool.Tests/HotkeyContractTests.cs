using System.Windows.Input;
using ScreenshotTool.Core.Hotkeys;

namespace ScreenshotTool.Tests;

internal static class HotkeyContractTests
{
    public static void ConfigurableHotkeysShouldParseAndRejectUnsafeValues()
    {
        if (!HotkeyGesture.TryParse("Ctrl+Shift+S", out var screenshot, out _) || screenshot.VirtualKey == 0)
            throw new InvalidOperationException("组合快捷键解析失败。");
        if (!HotkeyGesture.TryParse("F8", out var functionKey, out _) || functionKey.DisplayText != "F8")
            throw new InvalidOperationException("功能键解析失败。");
        if (HotkeyGesture.TryCreate(Key.A, ModifierKeys.None, out _, out _))
            throw new InvalidOperationException("不应允许无修饰键的字母占用全局输入。");
        if (!HotkeyGesture.TryParse(string.Empty, out _, out _))
            throw new InvalidOperationException("空快捷键应表示停用。");
    }
}
