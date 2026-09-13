using Microsoft.Win32;

namespace ScreenshotTool.App.Services;

public interface IStartupRegistrationService
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}

public sealed class StartupRegistrationService : IStartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "VioletSnap";
    private static readonly string[] LegacyValueNames = ["ZhouTianCapture", "ScreenshotTool"];

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return HasValue(key, ValueName) || LegacyValueNames.Any(name => HasValue(key, name));
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("无法打开 Windows 当前用户启动项。");
        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            foreach (var legacyValueName in LegacyValueNames)
                key.DeleteValue(legacyValueName, throwOnMissingValue: false);
            return;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
            throw new InvalidOperationException("无法获取当前程序路径。");
        foreach (var legacyValueName in LegacyValueNames)
            key.DeleteValue(legacyValueName, throwOnMissingValue: false);
        key.SetValue(ValueName, $"\"{executablePath}\"", RegistryValueKind.String);
    }

    private static bool HasValue(RegistryKey? key, string name) =>
        key?.GetValue(name) is string value && !string.IsNullOrWhiteSpace(value);
}
