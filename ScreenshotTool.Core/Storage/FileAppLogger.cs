using System.Globalization;
using System.IO;
using System.Text;
using ScreenshotTool.Core.Infrastructure;

namespace ScreenshotTool.Core.Storage;

public sealed class FileAppLogger : IAppLogger, IDisposable
{
    private readonly object _syncRoot = new();
    private readonly string _logDirectory;

    public FileAppLogger(string applicationDataDirectory)
    {
        _logDirectory = Path.Combine(applicationDataDirectory, "Logs");
    }

    public void Information(string message) => Write("INFO", message, null);

    public void Error(string message, Exception exception) => Write("ERROR", message, exception);

    public void Dispose()
    {
    }

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (_syncRoot)
            {
                Directory.CreateDirectory(_logDirectory);
                var path = Path.Combine(_logDirectory, $"VioletSnap_{DateTime.Now:yyyy-MM-dd}.log");
                var detail = exception is null ? string.Empty : $"{Environment.NewLine}{exception}";
                var line = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:O} [{1}] {2}{3}{4}",
                    DateTimeOffset.Now,
                    level,
                    message,
                    detail,
                    Environment.NewLine);
                File.AppendAllText(path, line, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            }
        }
        catch
        {
            // 日志写入失败不能导致常驻工具退出。
        }
    }
}
