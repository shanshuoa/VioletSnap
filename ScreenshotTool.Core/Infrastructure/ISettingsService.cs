using ScreenshotTool.Core.Configuration;

namespace ScreenshotTool.Core.Infrastructure;

public interface ISettingsService
{
    AppSettings Current { get; }
    void Load();
    void Save();
}
