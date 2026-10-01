using System.Diagnostics;
using System.IO;
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
    private const string ScheduledTaskName = "VioletSnap Startup";
    private const string ValueName = "VioletSnap";
    private static readonly string[] LegacyValueNames = ["ZhouTianCapture", "ScreenshotTool"];

    public bool IsEnabled()
    {
        if (RunScheduledTaskCommand(["/Query", "/TN", ScheduledTaskName]) == 0)
            return true;

        bool legacyEnabled;
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        legacyEnabled = HasValue(key, ValueName) || LegacyValueNames.Any(name => HasValue(key, name));
        if (!legacyEnabled)
            return false;

        try
        {
            SetEnabled(true);
            return RunScheduledTaskCommand(["/Query", "/TN", ScheduledTaskName]) == 0;
        }
        catch
        {
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("无法打开 Windows 当前用户启动项。");
        DeleteRegistryStartupValues(key);
        if (!enabled)
        {
            RunScheduledTaskCommand(["/Delete", "/TN", ScheduledTaskName, "/F"]);
            return;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
            throw new InvalidOperationException("无法获取当前程序路径。");
        var exitCode = RunScheduledTaskCommand([
            "/Create",
            "/TN", ScheduledTaskName,
            "/SC", "ONLOGON",
            "/RL", "HIGHEST",
            "/IT",
            "/TR", $"\"{executablePath}\"",
            "/F"
        ]);
        if (exitCode != 0)
            throw new InvalidOperationException($"无法创建最高权限开机启动任务，schtasks 返回 {exitCode}。");
    }

    private static void DeleteRegistryStartupValues(RegistryKey key)
    {
        key.DeleteValue(ValueName, throwOnMissingValue: false);
        foreach (var legacyValueName in LegacyValueNames)
            key.DeleteValue(legacyValueName, throwOnMissingValue: false);
    }

    private static int RunScheduledTaskCommand(IReadOnlyList<string> arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            if (process is null)
                return -1;
            if (!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree: true);
                return -2;
            }
            return process.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    private static bool HasValue(RegistryKey? key, string name) =>
        key?.GetValue(name) is string value && !string.IsNullOrWhiteSpace(value);
}
