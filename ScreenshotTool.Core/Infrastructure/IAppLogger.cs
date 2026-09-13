namespace ScreenshotTool.Core.Infrastructure;

public interface IAppLogger
{
    void Information(string message);
    void Error(string message, Exception exception);
}
