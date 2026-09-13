using System.Runtime.InteropServices;

namespace ScreenshotTool.Core.Clipboard;

public static class TextClipboard
{
    public static async Task CopyAsync(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        for (var attempt = 0; ; attempt++)
        {
            try { System.Windows.Clipboard.SetText(text); return; }
            catch (COMException) when (attempt < 4) { await Task.Delay(40 * (attempt + 1)); }
        }
    }
}
