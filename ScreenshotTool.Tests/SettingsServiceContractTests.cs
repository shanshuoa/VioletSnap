using ScreenshotTool.Core.Configuration;

namespace ScreenshotTool.Tests;

// Phase 1 仅保留无外部测试框架的契约检查，后续阶段接入完整测试运行器。
public static class SettingsServiceContractTests
{
    public static void DefaultSettingsShouldEnableClipboardCopy()
    {
        var settings = new AppSettings();
        if (!settings.Capture.DefaultCopyToClipboard)
        {
            throw new InvalidOperationException("默认配置应启用截图自动复制。");
        }
    }
}
